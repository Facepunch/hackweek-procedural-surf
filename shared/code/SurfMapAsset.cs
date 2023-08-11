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
		public Angles Angles { get; set; }
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

	public class SpawnPlatform : MapElement
	{
		public Vector3 Position { get; set; }
		public float Yaw { get; set; }
		public int Stage { get; set; }
	}

	public class Checkpoint : MapElement
	{
		public Vector3 Position { get; set; }
		public Angles Angles { get; set; }
		public int Stage { get; set; }
	}

	public string Title { get; set; } = "Untitled";
	public string Description { get; set; } = "No description";
	public string Author { get; set; }
	public DateTimeOffset Created { get; set; }
	public DateTimeOffset Modified { get; set; }
	public List<SupportBracket> SupportBrackets { get; set; }
	public List<BracketAttachment> BracketAttachments { get; set; }
	public List<TrackSection> TrackSections { get; set; }
	public List<SpawnPlatform> SpawnPlatforms { get; set; }
	public List<Checkpoint> Checkpoints { get; set; }

	[JsonIgnore]
	public bool IsUninitialized => SupportBrackets == null || BracketAttachments == null || TrackSections == null || SpawnPlatforms == null || Checkpoints == null;

	[JsonIgnore]
	public int ChangeIndex { get; private set; }

	protected override void PostReload()
	{
		++ChangeIndex;
	}
}
