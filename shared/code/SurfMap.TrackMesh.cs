using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Sandbox.Internal;
using static Sandbox.ParticleSnapshot;

namespace Sandbox.Surf;

partial class SurfMap
{
	partial class TrackSection
	{
		private Task _updateModelTask = Task.CompletedTask;
		private Mesh _mesh;
		private SceneObject _sceneObject;
		private PhysicsShape _physicsShape;

		public void UpdateModel()
		{
			ThreadSafe.AssertIsMainThread();

			_updateModelTask = UpdateModelAsync();
		}

		private record struct TrackStartEnd( Vector3 Position, Angles Angles, float Min, float Max, float TangentScale, bool Terminus );

		[StructLayout( LayoutKind.Sequential )]
		private record struct Vertex( Vector3 Position, Vector3 Normal, Vector4 Tangent, Vector2 TexCoord )
		{
			public static VertexAttribute[] Layout { get; } = new VertexAttribute[]
			{
				new ( VertexAttributeType.Position, VertexAttributeFormat.Float32 ),
				new ( VertexAttributeType.Normal, VertexAttributeFormat.Float32 ),
				new ( VertexAttributeType.Tangent, VertexAttributeFormat.Float32, 4 ),
				new ( VertexAttributeType.TexCoord, VertexAttributeFormat.Float32, 2 )
			};
		}

		[ThreadStatic]
		private static List<Vertex> TempRenderVertices;

		[ThreadStatic]
		private static List<int> TempRenderIndices;


		[ThreadStatic]
		private static List<Vector3> TempCollisionVertices;

		[ThreadStatic]
		private static List<int> TempCollisionIndices;

		private record struct CrossSectionVertex( float Anchor, Vector2 Offset, Vector2 Normal, float TexCoord, bool FanOrigin = false );

		private const float TerminusLength = 128f;
		private const float SkirtLength = 64f;
		private const float Thickness = 16f;
		private const float OuterCornerRadius = 16f;

		private static IEnumerable<(Vector2 Normal, float Along)> QuarterCircle( int segments )
		{
			for ( var i = 1; i < segments; ++i )
			{
				var t = i / (float)segments;
				var angle = t * MathF.PI * 0.5f;
				var cos = MathF.Cos( angle );
				var sin = MathF.Sin( angle );

				yield return (new Vector2( cos, sin ), t);
			}
		}

		private static IEnumerable<CrossSectionVertex> GenerateCrossSection()
		{
			var texCoordMargin = 0.125f;
			var texCoordHalfRounded = (OuterCornerRadius * MathF.PI * 0.25f / SkirtLength) * texCoordMargin;

			yield return new CrossSectionVertex( 0f, new Vector2( Thickness, -Thickness ), new Vector2( 1f, 0f ), texCoordMargin );
			yield return new CrossSectionVertex( 0f, new Vector2( Thickness, -SkirtLength ), new Vector2( 1f, 0f ), 0f );

			yield return new CrossSectionVertex( 0f, new Vector2( Thickness, -SkirtLength ), new Vector2( 0f, -1f ), 0f );
			yield return new CrossSectionVertex( 0f, new Vector2( 0f, -SkirtLength ), new Vector2( 0f, -1f ), 0f );

			yield return new CrossSectionVertex( 0f, new Vector2( 0f, -SkirtLength ), new Vector2( -1f, 0f ), 0f );
			yield return new CrossSectionVertex( 0f, new Vector2( 0f, -OuterCornerRadius ), new Vector2( -1f, 0f ), texCoordMargin - texCoordHalfRounded );

			foreach ( var (normal, t) in QuarterCircle( 4 ) )
			{
				var pos = OuterCornerRadius * new Vector2( 1f - normal.x, normal.y - 1f );
				var texCoord = t * texCoordHalfRounded * 2f + texCoordMargin - texCoordHalfRounded;
				yield return new CrossSectionVertex( 0f, pos, new Vector2( -normal.x, normal.y ), texCoord );
			}

			yield return new CrossSectionVertex( 0f, new Vector2( OuterCornerRadius, 0f ), new Vector2( 0f, 1f ), texCoordMargin + texCoordHalfRounded );
			yield return new CrossSectionVertex( 1f, new Vector2( -OuterCornerRadius, 0f ), new Vector2( 0f, 1f ), 1f - texCoordMargin - texCoordHalfRounded );

			foreach ( var (normal, t) in QuarterCircle( 4 ) )
			{
				var pos = OuterCornerRadius * new Vector2( normal.y - 1f, normal.x - 1f );
				var texCoord = t * texCoordHalfRounded * 2f + 1f - texCoordMargin - texCoordHalfRounded;
				yield return new CrossSectionVertex( 1f, pos, new Vector2( normal.y, normal.x ), texCoord );
			}

			yield return new CrossSectionVertex( 1f, new Vector2( 0f, -OuterCornerRadius ), new Vector2( 1f, 0f ), 1f - texCoordMargin + texCoordHalfRounded  );
			yield return new CrossSectionVertex( 1f, new Vector2( 0f, -SkirtLength ), new Vector2( 1f, 0f ), 1f );

			yield return new CrossSectionVertex( 1f, new Vector2( 0f, -SkirtLength ), new Vector2( 0f, -1f ), 1f );
			yield return new CrossSectionVertex( 1f, new Vector2( -Thickness, -SkirtLength ), new Vector2( 0f, -1f ), 1f );

			yield return new CrossSectionVertex( 1f, new Vector2( -Thickness, -SkirtLength ), new Vector2( -1f, 0f ), 1f );
			yield return new CrossSectionVertex( 1f, new Vector2( -Thickness, -Thickness ), new Vector2( -1f, 0f ), 1f - texCoordMargin );

			yield return new CrossSectionVertex( 1f, new Vector2( -Thickness, -Thickness ), new Vector2( 0f, -1f ), 1f - texCoordMargin, true );
			yield return new CrossSectionVertex( 0f, new Vector2( Thickness, -Thickness ), new Vector2( 0f, -1f ), texCoordMargin, true );
		}

		private async Task UpdateModelAsync()
		{
			var start = new TrackStartEnd( Start.Bracket.Position, Start.Bracket.Angles,
				Start.Min, Start.Max, Start.TangentScale, Start.TrackSections.Count == 1 );

			var end = new TrackStartEnd( End.Bracket.Position, End.Bracket.Angles,
				End.Min, End.Max, End.TangentScale, End.TrackSections.Count == 1 );

			try
			{
				if ( _updateModelTask != null && !_updateModelTask.IsCompleted )
				{
					await _updateModelTask;
				}
			}
			catch ( Exception e )
			{
				Log.Error( e );
			}

			if ( !IsValid )
			{
				return;
			}

			await GameTask.WorkerThread();

			var renderVerts = TempRenderVertices ??= new List<Vertex>();
			var renderIndices = TempRenderIndices ??= new List<int>();

			var collisionVerts = TempCollisionVertices ??= new List<Vector3>();
			var collisionIndices = TempCollisionIndices ??= new List<int>();

			renderVerts.Clear();
			renderIndices.Clear();

			collisionVerts.Clear();
			collisionIndices.Clear();

			const int segments = 16;

			var crossSection = GenerateCrossSection().ToArray();
			var crossSectionVertices = crossSection.Length;

			var dist = (end.Position - start.Position).Length;

			var startRot = SupportBracket.RotationFromAngles( start.Angles );
			var endRot = SupportBracket.RotationFromAngles( end.Angles );

			var p0 = start.Position;
			var p1 = start.Position + startRot.Forward * dist * start.TangentScale;
			var p2 = end.Position - endRot.Forward * dist * end.TangentScale;
			var p3 = end.Position;

			var dt = 1f / segments;

			// Estimate track section length

			var startPos = start.Terminus ? p0 + -startRot.Forward * TerminusLength : p0;
			var endPos = end.Terminus ? p3 + endRot.Forward * TerminusLength : p3;

			var length = 0f;
			var prevPos = p0;

			var minI = start.Terminus ? -1 : 0;
			var maxI = end.Terminus ? segments + 1 : segments;

			for ( var i = 1; i <= maxI; ++i )
			{
				var t = i * dt;
				var nextPos = Vector3.CubicBeizer( p0, p3, p1, p2, t );

				length += (nextPos - prevPos).Length;

				prevPos = nextPos;
			}

			var vScale = Math.Max( 1f, MathF.Round( length / 512f ) ) / length;

			length = -(startPos - p0).Length;
			prevPos = startPos;

			for ( var i = minI; i <= maxI; ++i )
			{
				Rotation rotation;
				Vector3 nextPos;

				var t = i * dt;

				if ( i <= 0 )
				{
					rotation = startRot;
					nextPos = p0 + i * startRot.Forward * TerminusLength;
				}
				else if ( i >= segments )
				{
					rotation = endRot;
					nextPos = p3 + (i - segments) * endRot.Forward * TerminusLength;
				}
				else
				{
					nextPos = Vector3.CubicBeizer( p0, p3, p1, p2, t );

					var roll = MathX.Lerp( start.Angles.roll, end.Angles.roll, t );

					var prev = Vector3.CubicBeizer( p0, p3, p1, p2, t - dt * 0.5f );
					var next = Vector3.CubicBeizer( p0, p3, p1, p2, t + dt * 0.5f );

					var forward = (next - prev).Normal;

					if ( MathF.Abs( forward.z ) <= 0f )
					{
						var yaw = MathF.Atan2( forward.y, forward.x ) * 180f / MathF.PI;
						rotation = Rotation.FromYaw( yaw ) * Rotation.FromRoll( roll );
					}
					else
					{
						rotation = Rotation.LookAt( forward ) * Rotation.FromRoll( roll );
					}
				}

				length += (nextPos - prevPos).Length;
				prevPos = nextPos;

				var right = rotation.Right;
				var up = rotation.Up;

				var min = MathX.Lerp( start.Min, end.Min, t );
				var max = MathX.Lerp( start.Max, end.Max, t );

				collisionVerts.Add( nextPos + right * min );
				collisionVerts.Add( nextPos + right * max );
				collisionVerts.Add( nextPos + right * min - up * SkirtLength );
				collisionVerts.Add( nextPos + right * max - up * SkirtLength );

				foreach ( var vertex in crossSection )
				{
					var vertPos = nextPos + right * (vertex.Offset.x + MathX.Lerp( min, max, vertex.Anchor )) + up * vertex.Offset.y;
					var normal = (right * vertex.Normal.x + up * vertex.Normal.y).Normal;
					var tangent = rotation.Forward;

					renderVerts.Add( new Vertex( vertPos, normal,
						new Vector4( tangent, 1f ),
						new Vector2( length * vScale, vertex.TexCoord ) ) );
				}
			}

			static void AddQuad( List<int> indices, int i0, int i1, int i2, int i3 )
			{
				indices.Add( i0 );
				indices.Add( i1 );
				indices.Add( i2 );

				indices.Add( i2 );
				indices.Add( i1 );
				indices.Add( i3 );
			}

			for ( var i = 1; i < crossSection.Length; i++ )
			{
				var a = crossSection[i - 1];
				var b = crossSection[i];

				if ( Math.Abs( a.Anchor - b.Anchor ) < 0.0001f && a.Offset.AlmostEqual( b.Offset ) )
				{
					continue;
				}

				var i0 = i - 1;
				var i1 = i;

				var i2 = i0 + crossSectionVertices;
				var i3 = i1 + crossSectionVertices;

				for ( int j = minI, index = 0; j < maxI; j++, index += crossSectionVertices )
				{
					AddQuad( renderIndices, index + i0, index + i1, index + i2, index + i3 );
				}
			}

			for ( int i = minI, index = 0; i < maxI; i++, index += 4 )
			{
				AddQuad( collisionIndices, index + 0, index + 1, index + 4, index + 5 );
				AddQuad( collisionIndices, index + 1, index + 3, index + 5, index + 7 );
				AddQuad( collisionIndices, index + 3, index + 2, index + 7, index + 6 );
				AddQuad( collisionIndices, index + 2, index + 0, index + 6, index + 4 );
			}

			static void GenerateEndCap( List<Vertex> renderVerts, List<int> renderIndices, CrossSectionVertex[] crossSection, Vector3 pos, Rotation rot, float min, float max, bool flip )
			{
				var indexOffset = renderVerts.Count;
				var leftFanCenterIndex = 0;
				var rightFanCenterIndex = 0;
				var firstRightIndex = -1;

				foreach ( var vertex in crossSection )
				{
					var right = rot.Right;
					var up = rot.Up;

					var along = MathX.Lerp( min, max, vertex.Anchor );
					var vertPos = pos + right * (vertex.Offset.x + along) + up * vertex.Offset.y;

					if ( vertex.FanOrigin )
					{
						if ( vertex.Anchor < 0.5f ) leftFanCenterIndex = renderVerts.Count;
						else rightFanCenterIndex = renderVerts.Count;
					}

					if ( vertex.Anchor >= 0.5f && firstRightIndex == -1 )
					{
						firstRightIndex = renderVerts.Count;
					}

					renderVerts.Add( new Vertex( vertPos, rot.Forward * (flip ? 1f : -1f),
						new Vector4( right, 1f ), new Vector2( along / (max - min), vertex.Offset.y * 0.125f ) ) );
				}

				var prevVertex = crossSection[^1];
				var prevIndex = crossSection.Length - 1;

				for ( var i = 0; i < crossSection.Length; ++i )
				{
					var nextVertex = crossSection[i];

					if ( prevVertex.FanOrigin && nextVertex.FanOrigin )
					{
						renderIndices.Add( leftFanCenterIndex );
						renderIndices.Add( flip ? firstRightIndex : rightFanCenterIndex );
						renderIndices.Add( flip ? rightFanCenterIndex : firstRightIndex );
					}
					else if ( !prevVertex.FanOrigin && !nextVertex.FanOrigin )
					{
						renderIndices.Add( prevVertex.Anchor < 0.5f ? leftFanCenterIndex : rightFanCenterIndex );
						renderIndices.Add( indexOffset + (flip ? prevIndex : i) );
						renderIndices.Add( indexOffset + (flip ? i : prevIndex) );
					}

					prevVertex = nextVertex;
					prevIndex = i;
				}
			}

			if ( start.Terminus )
			{
				AddQuad( collisionIndices, 0, 1, 2, 3 );
				GenerateEndCap( renderVerts, renderIndices, crossSection, startPos, startRot, start.Min, start.Max, false );
			}

			if ( end.Terminus )
			{
				var lastFaceIndex = collisionVerts.Count - 4;
				AddQuad( collisionIndices, lastFaceIndex + 1, lastFaceIndex + 0, lastFaceIndex + 3, lastFaceIndex + 2 );
				GenerateEndCap( renderVerts, renderIndices, crossSection, endPos, endRot, start.Min, start.Max, true );
			}

			var newMesh = _mesh == null;

			_mesh ??= new Mesh( Material );

			if ( !_mesh.HasVertexBuffer )
			{
				_mesh.CreateVertexBuffer( renderVerts.Count, Vertex.Layout, renderVerts );
				_mesh.CreateIndexBuffer( renderIndices.Count, renderIndices );
			}
			else
			{
				_mesh.SetVertexBufferSize( renderVerts.Count );
				_mesh.SetIndexBufferSize( renderIndices.Count );

				_mesh.SetVertexBufferData( renderVerts );
				_mesh.SetIndexBufferData( renderIndices );

				_mesh.SetIndexRange( 0, renderIndices.Count );
			}

			await GameTask.MainThread();

			if ( _sceneObject == null )
			{
				var model = new ModelBuilder()
					.AddMesh( _mesh )
					.Create();

				_sceneObject = AddSceneObject( model );
			}
			else if ( newMesh )
			{
				_sceneObject.Model = new ModelBuilder()
					.AddMesh( _mesh )
					.Create();
			}

			if ( PhysicsBody == null )
			{
				return;
			}

			if ( _physicsShape == null )
			{
				_physicsShape = AddMeshShape( collisionVerts, collisionIndices );
			}
			else
			{
				_physicsShape.UpdateMesh( collisionVerts, collisionIndices );
			}
		}
	}
}
