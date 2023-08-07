using System;
using System.Collections.Generic;

namespace Sandbox.Surf;

[GameResource("Surf Map", "surf", "A bunch of surf ramps", Icon = "surfing" )]
public partial class SurfMap : GameResource
{
	public class Ramp
	{
		public List<Node> Nodes { get; set; }

		public void DrawDebug()
		{
			if ( Nodes is not { Count: > 0 } )
			{
				return;
			}

			for ( var i = 0; i <= (Nodes.Count - 1) * 16; ++i )
			{
				var index = i / 16f;

				var prev = Nodes[Math.Clamp( (int)MathF.Floor( index ), 0, Nodes.Count - 1 )];
				var next = Nodes[Math.Clamp( (int)MathF.Ceiling( index ), 0, Nodes.Count - 1 )];

				var t = index - MathF.Floor( index );

				Node.CubicBeizer( in prev, in next, t ).DrawDebug();
			}
		}
	}

	public struct Node
	{
		public Vector3 Position { get; set; }
		public Rotation Rotation { get; set; }
		public float Tangent { get; set; }

		public float Width { get; set; }
		public float Height { get; set; }

		public static Node CubicBeizer( in Node a, in Node b, float t )
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

		public void DrawDebug()
		{
			var up = Rotation.Up;
			var right = Rotation.Right;

			var top = Position;
			var bl = top - up * Height - right * Width * 0.5f;
			var br = bl + right * Width;

			DebugOverlay.Line( top, br );
			DebugOverlay.Line( top, bl );
		}
	}

	[HideInEditor]
	public List<Ramp> Ramps { get; set; }

	public void DrawDebug()
	{
		if ( Ramps is not { Count: > 0 } )
		{
			return;
		}

		foreach ( var ramp in Ramps )
		{
			ramp.DrawDebug();
		}
	}
}
