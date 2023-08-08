using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox.Surf;

[GameResource("Surf Map", "surf", "A bunch of surf ramps", Icon = "surfing" )]
public partial class SurfMap : GameResource
{
	public class MapElement
	{
		public int Id { get; init; }
	}

	public class SupportBracket : MapElement
	{
		public Vector3 Position { get; set; }
		public float Yaw { get; set; }
		public float Roll { get; set; } = 60f;
	}

	public class BracketAttachment : MapElement
	{
		public int BracketId { get; set; }
		public float TangentScale { get; set; } = 0.5f;
		public float Min { get; set; } = -128f;
		public float Max { get; set; } = 128f;
	}

	public class TrackSection : MapElement
	{
		public int StartId { get; set; }
		public int EndId { get; set; }
	}

	private int _nextSupportBracketId;
	private int _nextBracketAttachmentId;
	private int _nextTrackSectionId;

	private readonly Dictionary<int, SupportBracket> _supportBrackets = new Dictionary<int, SupportBracket>();
	private readonly Dictionary<int, BracketAttachment> _bracketAttachments = new Dictionary<int, BracketAttachment>();
	private readonly Dictionary<int, TrackSection> _trackSections = new Dictionary<int, TrackSection>();

	[HideInEditor]
	public List<SupportBracket> SupportBrackets
	{
		get => _supportBrackets.Values.ToList();
		set => SetElements( _supportBrackets, ref _nextSupportBracketId, value );
	}

	[HideInEditor] public List<BracketAttachment> BracketAttachments
	{
		get => _bracketAttachments.Values.ToList();
		set => SetElements( _bracketAttachments, ref _nextBracketAttachmentId, value );
	}

	[HideInEditor] public List<TrackSection> TrackSections
	{
		get => _trackSections.Values.ToList();
		set => SetElements( _trackSections, ref _nextTrackSectionId, value );
	}

	[HideInEditor] public bool IsUninitialized => _nextSupportBracketId == 0 && _nextBracketAttachmentId == 0 && _nextTrackSectionId == 0;

	private static void SetElements<T>( Dictionary<int, T> dict, ref int nextId, List<T> list )
		where T : MapElement
	{
		dict.Clear();
		nextId = 0;

		if ( list == null )
		{
			return;
		}

		foreach ( var item in list )
		{
			dict.Add( item.Id, item );
			nextId = Math.Max( nextId, item.Id + 1 );
		}
	}

	private static T AddElement<T>( Dictionary<int, T> dict, ref int nextId )
		where T : MapElement, new()
	{
		var elem = new T { Id = nextId++ };
		dict.Add( elem.Id, elem );
		return elem;
	}

	public SupportBracket AddSupportBracket()
	{
		return AddElement( _supportBrackets, ref _nextSupportBracketId );
	}

	public BracketAttachment AddBracketAttachment( SupportBracket bracket )
	{
		var attachment = AddElement( _bracketAttachments, ref _nextBracketAttachmentId );

		attachment.BracketId = bracket.Id;

		return attachment;
	}

	public TrackSection AddTrackSection( BracketAttachment start, BracketAttachment end )
	{
		var track = AddElement( _trackSections, ref _nextTrackSectionId );

		track.StartId = start.Id;
		track.EndId = end.Id;

		return track;
	}

	public void RemoveSupportBracket( SupportBracket bracket )
	{
		_supportBrackets.Remove( bracket.Id );

		var attachments = _bracketAttachments.Values
			.Where( x => x.BracketId == bracket.Id )
			.ToArray();

		foreach ( var attachment in attachments )
		{
			RemoveBracketAttachment( attachment );
		}
	}

	public void RemoveBracketAttachment( BracketAttachment attachment )
	{
		_bracketAttachments.Remove( attachment.Id );

		var tracks = _trackSections.Values
			.Where( x => x.StartId == attachment.Id || x.EndId == attachment.Id )
			.ToArray();

		foreach ( var track in tracks )
		{
			RemoveTrackSection( track );
		}
	}

	public void RemoveTrackSection( TrackSection track )
	{
		_trackSections.Remove( track.Id );
	}
}
