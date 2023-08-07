using System;
using System.Collections.Generic;

namespace Sandbox.Surf;

[GameResource("Surf Map", "surf", "A bunch of surf ramps", Icon = "surfing" )]
public partial class SurfMap : GameResource
{
	public class Ramp
	{
		public List<Node> Nodes { get; set; }
	}

	public struct Node
	{
		public Vector3 Position { get; set; }
		public float Yaw { get; set; }
		public float Pitch { get; set; }
		public float Tangent { get; set; }

		public float Width { get; set; }
		public float Height { get; set; }

		public static Node CubicBeizer( in Node a, in Node b, float t )
		{
			var aRot = Rotation.From( a.Pitch, a.Yaw, 0f );
			var bRot = Rotation.From( b.Pitch, b.Yaw, 0f );

			var p0 = a.Position;
			var p1 = a.Position + aRot.Forward * a.Tangent;
			var p2 = b.Position - bRot.Forward * b.Tangent;
			var p3 = b.Position;

			var prev = Vector3.CubicBeizer( p0, p3, p1, p2, t - 1f / 16f );
			var next = Vector3.CubicBeizer( p0, p3, p1, p2, t + 1f / 16f );

			var forward = (next - prev).Normal;
			var rot = Rotation.LookAt( forward );

			return new Node
			{
				Position = Vector3.CubicBeizer( p0, p3, p1, p2, t ),
				Yaw = rot.Yaw(),
				Pitch = rot.Pitch(),
				Tangent = MathX.Lerp( a.Tangent, b.Tangent, t ),
				Width = MathX.Lerp( a.Width, b.Width, t ),
				Height = MathX.Lerp( a.Width, b.Width, t )
			};
		}
	}

	[HideInEditor]
	public List<Ramp> Ramps { get; set; }
}
