using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Sandbox.Internal;

namespace Sandbox.Surf;

partial class SurfMap
{
	partial class TrackSection
	{
		private Task _updateModelTask = Task.CompletedTask;
		private Mesh _mesh;

		public void UpdateModel()
		{
			ThreadSafe.AssertIsMainThread();

			_updateModelTask = UpdateModelAsync();
		}

		private record struct TrackStartEnd( Vector3 Position, Angles Angles, float Min, float Max, float TangentScale );

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

		private record struct CrossSectionVertex( float Anchor, Vector2 Offset, Vector2 Normal, float TexCoord );

		private const float SkirtLength = 64f;
		private const float Thickness = 16f;
		private const float OuterCornerRadius = 16f;
		private static CrossSectionVertex[] CrossSection { get; } = GenerateCrossSection().ToArray();

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
		}

		private async Task UpdateModelAsync()
		{
			var start = new TrackStartEnd( Start.Bracket.Position, Start.Bracket.Angles,
				Start.Min, Start.Max, Start.TangentScale );

			var end = new TrackStartEnd( End.Bracket.Position, End.Bracket.Angles,
				End.Min, End.Max, End.TangentScale );

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

			var crossSectionVertices = CrossSection.Length;

			var dist = (end.Position - start.Position).Length;

			var startRot = SupportBracket.RotationFromAngles( start.Angles );
			var endRot = SupportBracket.RotationFromAngles( end.Angles );

			var p0 = start.Position;
			var p1 = start.Position + startRot.Forward * dist * start.TangentScale;
			var p2 = end.Position - endRot.Forward * dist * end.TangentScale;
			var p3 = end.Position;

			var dt = 1f / segments;

			for ( var i = 0; i <= segments; ++i )
			{
				var t = i * dt;

				var pos = Vector3.CubicBeizer( p0, p3, p1, p2, t );

				Rotation rotation;

				if ( i == 0 )
				{
					rotation = startRot;
				}
				else if ( i == segments )
				{
					rotation = endRot;
				}
				else
				{
					var roll = MathX.Lerp( start.Angles.roll, end.Angles.roll, t );

					var prev = Vector3.CubicBeizer( p0, p3, p1, p2, t - dt * 0.5f );
					var next = Vector3.CubicBeizer( p0, p3, p1, p2, t + dt * 0.5f );

					var forward = (next - prev).Normal;
					rotation = Rotation.LookAt( forward ) * Rotation.FromRoll( roll );
				}

				var right = rotation.Right;
				var up = rotation.Up;

				var min = MathX.Lerp( start.Min, end.Min, t );
				var max = MathX.Lerp( start.Max, end.Max, t );

				collisionVerts.Add( pos + right * min );
				collisionVerts.Add( pos + right * max );
				collisionVerts.Add( pos + right * min - up * SkirtLength );
				collisionVerts.Add( pos + right * max - up * SkirtLength );

				foreach ( var vertex in CrossSection )
				{
					var vertPos = pos + right * (vertex.Offset.x + MathX.Lerp( min, max, vertex.Anchor )) + up * vertex.Offset.y;
					var normal = (right * vertex.Normal.x + up * vertex.Normal.y).Normal;
					var tangent = rotation.Forward;

					renderVerts.Add( new Vertex( vertPos, normal,
						new Vector4( tangent, 1f ),
						new Vector2( t, vertex.TexCoord ) ) );
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

			for ( var i = 1; i < CrossSection.Length; i++ )
			{
				var a = CrossSection[i - 1];
				var b = CrossSection[i];

				if ( Math.Abs( a.Anchor - b.Anchor ) < 0.0001f && a.Offset.AlmostEqual( b.Offset ) )
				{
					continue;
				}

				var i0 = i - 1;
				var i1 = i;

				var i2 = i0 + crossSectionVertices;
				var i3 = i1 + crossSectionVertices;

				for ( int j = 0, index = 0; j < segments; j++, index += crossSectionVertices )
				{
					AddQuad( renderIndices, index + i0, index + i1, index + i2, index + i3 );
				}
			}

			for ( int i = 0, index = 0; i < segments; i++, index += 4 )
			{
				AddQuad( collisionIndices, index + 0, index + 1, index + 4, index + 5 );
				AddQuad( collisionIndices, index + 1, index + 3, index + 5, index + 7 );
				AddQuad( collisionIndices, index + 3, index + 2, index + 7, index + 6 );
				AddQuad( collisionIndices, index + 2, index + 0, index + 6, index + 4 );
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

			if ( SceneObject == null )
			{
				var model = new ModelBuilder()
					.AddMesh( _mesh )
					.Create();

				SceneObject = new SceneObject( World, model );
			}
			else if ( newMesh )
			{
				SceneObject.Model = new ModelBuilder()
					.AddMesh( _mesh )
					.Create();
			}
		}
	}
}
