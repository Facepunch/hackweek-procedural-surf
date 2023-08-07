using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Sandbox.Surf.SurfRamp;

namespace Sandbox.Surf
{
	[GameResource("Surf Ramp", "surf", "A surf ramp spline", Icon = "surfing" )]
	public partial class SurfRamp : GameResource
	{
		public struct Node
		{
			public Vector3 Position { get; set; }
			public Rotation Rotation { get; set; }
			public float Tangent { get; set; }

			public float Width { get; set; }
			public float Height { get; set; }
		}

		public List<Node> Nodes { get; set; }

		public void DrawDebug()
		{
			if ( Nodes is not { Count: > 0 } )
			{
				return;
			}

			for ( var i = 0; i <= (Nodes.Count - 1) * 16; ++i )
			{
				DrawDebug( i / 16f );
			}
		}

		public Node Interpolate( in Node a, in Node b, float t )
		{
			return new Node
			{
				Position = Vector3.CubicBeizer( a.Position, b.Position,
					a.Position + a.Rotation.Forward * a.Tangent,
					b.Position - b.Rotation.Forward * b.Tangent, t ),
				Rotation = Rotation.Slerp( a.Rotation, b.Rotation, t ),
				Tangent = MathX.Lerp( a.Tangent, b.Tangent, t ),
				Width = MathX.Lerp( a.Width, b.Width, t ),
				Height = MathX.Lerp( a.Width, b.Width, t )
			};
		}

		public void DrawDebug( float index )
		{
			var prev = Nodes[Math.Clamp( (int)MathF.Floor( index ), 0, Nodes.Count - 1 )];
			var next = Nodes[Math.Clamp( (int)MathF.Ceiling( index ), 0, Nodes.Count - 1 )];

			var t = index - MathF.Floor( index );

			var node = Interpolate( prev, next, t );

			var up = node.Rotation.Up;
			var right = node.Rotation.Right;

			var top = node.Position;
			var bl = top - up * node.Height - right * node.Width * 0.5f;
			var br = bl + right * node.Width;

			DebugOverlay.Line( top, br );
			DebugOverlay.Line( top, bl );
		}
	}
}
