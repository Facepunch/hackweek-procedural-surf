using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox.Surf;

public partial class SurfMap
{
	public class MapElement : IValid
	{
		public int Id { get; init; }
		public bool IsValid { get; private set; } = true;

		public SceneWorld World { get; init; }
		public SceneObject SceneObject { get; protected set; }

		public void Removed()
		{
			IsValid = false;

			SceneObject?.Delete();
			SceneObject = null;

			OnRemoved();
		}

		protected virtual void OnRemoved()
		{

		}
	}

	public class SupportBracket : MapElement
	{
		public Vector3 Position { get; set; } = new Vector3( 0f, 0f, 512f );
		public float Yaw { get; set; }
		public float Roll { get; set; } = 50f;

		public Rotation Rotation => Rotation.From( 0f, Yaw, Roll );

		public List<BracketAttachment> Attachments { get; } = new List<BracketAttachment>();
	}

	public class BracketAttachment : MapElement
	{
		private SupportBracket _bracket;

		public SupportBracket Bracket
		{
			get => _bracket;
			set
			{
				_bracket?.Attachments.Remove( this );

				if ( !value.IsValid() )
				{
					throw new Exception();
				}

				_bracket = value;
				_bracket.Attachments.Add( this );
			}
		}

		public float TangentScale { get; set; } = 0.5f;
		public float Min { get; set; } = -256f;
		public float Max { get; set; } = 256f;

		public List<TrackSection> TrackSections { get; } = new List<TrackSection>();

		protected override void OnRemoved()
		{
			if ( _bracket.IsValid() )
			{
				_bracket.Attachments.Remove( this );
				_bracket = null;
			}
		}
	}

	public partial class TrackSection : MapElement
	{
		private BracketAttachment _start;
		private BracketAttachment _end;

		public BracketAttachment Start
		{
			get => _start;
			set
			{
				_start?.TrackSections.Remove( this );

				if ( !value.IsValid() || value == _end )
				{
					throw new Exception();
				}

				_start = value;
				_start.TrackSections.Add( this );
			}
		}

		public BracketAttachment End
		{
			get => _end;
			set
			{
				_end?.TrackSections.Remove( this );

				if ( !value.IsValid() || value == _start )
				{
					throw new Exception();
				}

				_end = value;
				_end.TrackSections.Add( this );
			}
		}

		public Material Material { get; set; }

		protected override void OnRemoved()
		{
			if ( _start.IsValid() )
			{
				_start.TrackSections.Remove( this );
			}

			if ( _end.IsValid() )
			{
				_end.TrackSections.Remove( this );
			}

			_start = null;
			_end = null;
		}
	}

	private int _nextSupportBracketId;
	private int _nextBracketAttachmentId;
	private int _nextTrackSectionId;

	private readonly Dictionary<int, SupportBracket> _supportBrackets = new Dictionary<int, SupportBracket>();
	private readonly Dictionary<int, BracketAttachment> _bracketAttachments = new Dictionary<int, BracketAttachment>();
	private readonly Dictionary<int, TrackSection> _trackSections = new Dictionary<int, TrackSection>();

	public SceneWorld World { get; }
	public Material DefaultTrackMaterial { get; set; } = Material.Load( "materials/surf/track_default.vmat" );

	public SurfMap( SceneWorld world )
	{
		World = world;
	}

	public IEnumerable<SupportBracket> SupportBrackets => _supportBrackets.Values;

	public IEnumerable<BracketAttachment> BracketAttachments => _bracketAttachments.Values;

	public IEnumerable<TrackSection> TrackSections => _trackSections.Values;

	private T AddElement<T>( Dictionary<int, T> dict, ref int nextId )
		where T : MapElement, new()
	{
		var elem = new T { Id = nextId++, World = World };
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

		attachment.Bracket = bracket;

		return attachment;
	}

	public TrackSection AddTrackSection( BracketAttachment start, BracketAttachment end )
	{
		var track = AddElement( _trackSections, ref _nextTrackSectionId );

		track.Start = start;
		track.End = end;

		return track;
	}

	public void RemoveSupportBracket( SupportBracket bracket )
	{
		_supportBrackets.Remove( bracket.Id );
		bracket.Removed();

		foreach ( var attachment in bracket.Attachments.ToArray() )
		{
			RemoveBracketAttachment( attachment );
		}
	}

	public void RemoveBracketAttachment( BracketAttachment attachment )
	{
		_bracketAttachments.Remove( attachment.Id );
		attachment.Removed();

		foreach ( var track in attachment.TrackSections.ToArray() )
		{
			RemoveTrackSection( track );
		}
	}

	public void RemoveTrackSection( TrackSection track )
	{
		_trackSections.Remove( track.Id );
		track.Removed();
	}

	public void Load( SurfMapAsset asset )
	{
		_nextSupportBracketId = 0;
		_nextBracketAttachmentId = 0;
		_nextTrackSectionId = 0;

		_supportBrackets.Clear();
		_bracketAttachments.Clear();
		_trackSections.Clear();

		if ( asset.IsUninitialized )
		{
			var supportA = AddSupportBracket();
			var supportB = AddSupportBracket();

			supportA.Position = new Vector3( -1024f, 0f, 512f );
			supportB.Position = new Vector3( 1024f, 0f, 512f );

			var attachA = AddBracketAttachment( supportA );
			var attachB = AddBracketAttachment( supportB );

			var track = AddTrackSection( attachA, attachB );

			track.Material = DefaultTrackMaterial;

			return;
		}

		foreach ( var bracket in asset.SupportBrackets )
		{
			_supportBrackets.Add( bracket.Id,
				new SupportBracket
				{
					Id = bracket.Id, World = World, Position = bracket.Position, Roll = bracket.Roll, Yaw = bracket.Yaw
				} );
			_nextSupportBracketId = Math.Max( _nextSupportBracketId, bracket.Id + 1 );
		}

		foreach ( var attachment in asset.BracketAttachments )
		{
			_bracketAttachments.Add( attachment.Id,
				new BracketAttachment
				{
					Id = attachment.Id,
					World = World,
					Bracket = _supportBrackets[attachment.BracketId],
					TangentScale = attachment.TangentScale,
					Min = attachment.Min,
					Max = attachment.Max
				} );
			_nextBracketAttachmentId = Math.Max( _nextBracketAttachmentId, attachment.Id + 1 );
		}

		foreach ( var track in asset.TrackSections )
		{
			_trackSections.Add( track.Id,
				new TrackSection
				{
					Id = track.Id,
					World = World,
					Start = _bracketAttachments[track.StartId],
					End = _bracketAttachments[track.EndId],
					Material = track.Material
				} );
			_nextTrackSectionId = Math.Max( _nextTrackSectionId, track.Id + 1 );
		}

		foreach ( var track in _trackSections )
		{
			track.Value.UpdateModel();
		}
	}

	public void Save( SurfMapAsset asset )
	{
		asset.SupportBrackets = SupportBrackets.Select( x => new SurfMapAsset.SupportBracket
		{
			Id = x.Id,
			Position = x.Position,
			Roll = x.Roll,
			Yaw = x.Yaw
		} ).ToList();

		asset.BracketAttachments = BracketAttachments.Select( x => new SurfMapAsset.BracketAttachment
		{
			Id = x.Id,
			BracketId = x.Bracket.Id,
			TangentScale = x.TangentScale,
			Min = x.Min,
			Max = x.Max
		} ).ToList();

		asset.TrackSections = TrackSections.Select( x => new SurfMapAsset.TrackSection
		{
			Id = x.Id, StartId = x.Start.Id, EndId = x.End.Id, Material = x.Material
		} ).ToList();
	}
}
