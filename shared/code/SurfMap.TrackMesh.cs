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
		private static List<Vertex> TempVertices;

		[ThreadStatic]
		private static List<int> TempIndices;

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

			var verts = TempVertices ??= new List<Vertex>();
			var indices = TempIndices ??= new List<int>();

			verts.Clear();
			indices.Clear();

			const int segments = 16;

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

				verts.Add( new Vertex( pos + right * min, up, new Vector4( right, 1f ), new Vector2( 0f, t ) ) );
				verts.Add( new Vertex( pos + right * max, up, new Vector4( right, 1f ), new Vector2( 1f, t ) ) );
			}

			for ( int i = 0, index = 0; i < segments; i++, index += 2 )
			{
				indices.Add( index + 0 );
				indices.Add( index + 1 );
				indices.Add( index + 2 );

				indices.Add( index + 2 );
				indices.Add( index + 1 );
				indices.Add( index + 3 );
			}

			var newMesh = _mesh == null;

			_mesh ??= new Mesh( Material );

			if ( !_mesh.HasVertexBuffer )
			{
				_mesh.CreateVertexBuffer( verts.Count, Vertex.Layout, verts );
				_mesh.CreateIndexBuffer( indices.Count, indices );
			}
			else
			{
				_mesh.SetVertexBufferSize( verts.Count );
				_mesh.SetIndexBufferSize( indices.Count );

				_mesh.SetVertexBufferData( verts );
				_mesh.SetIndexBufferData( indices );

				_mesh.SetIndexRange( 0, indices.Count );
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
