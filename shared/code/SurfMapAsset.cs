using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Sandbox.Surf;

[GameResource("Surf Map", "surf", "A bunch of surf ramps", Icon = "surfing" )]
public partial class SurfMapAsset : GameResource
{
	public class MapElement
	{
		public int Id { get; set; }
	}

	public class SupportBracket : MapElement
	{
		public Vector3 Position { get; set; } = new Vector3( 0f, 0f, 512f );
		public float Yaw { get; set; }
		public float Roll { get; set; } = 50f;
	}

	public class BracketAttachment : MapElement
	{
		public int BracketId { get; set; }
		public float TangentScale { get; set; } = 0.5f;
		public float Min { get; set; } = -256f;
		public float Max { get; set; } = 256f;
	}

	public class TrackSection : MapElement
	{
		public int StartId { get; set; }
		public int EndId { get; set; }
		public Material Material { get; set; }
	}
	
	public List<SupportBracket> SupportBrackets { get; set; }
	public List<BracketAttachment> BracketAttachments { get; set; }
	public List<TrackSection> TrackSections { get; set; }

	[JsonIgnore]
	public bool IsUninitialized => SupportBrackets == null || BracketAttachments == null || TrackSections == null;
}
