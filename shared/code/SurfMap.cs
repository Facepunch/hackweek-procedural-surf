using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sandbox.Diagnostics;

namespace Sandbox.Surf;

public partial class SurfMap
{
	public interface IMapElement
	{
		int Id { get; }
		bool IsValid { get; }

		void Changed();
	}

	public class MapModel
	{
		private readonly Entity _entity;
		private readonly SceneObject _sceneObject;

		public MapModel( Entity entity )
		{
			_entity = entity;
			_sceneObject = null;
		}

		public MapModel( SceneObject sceneObject )
		{
			_sceneObject = sceneObject;
			_entity = null;
		}

		public void Delete()
		{
			_entity?.Delete();
			_sceneObject?.Delete();
		}

		public Transform Transform
		{
			get => _entity?.Transform ?? _sceneObject?.Transform ?? Transform.Zero;
			set
			{
				if ( _entity != null )
				{
					_entity.Transform = value;
				}

				if ( _sceneObject != null )
				{
					_sceneObject.Transform = value;
				}
			}
		} 
	}

	public class SupportPillar
	{
		private readonly MapElement _owner;
		private readonly List<MapModel> _pillars = new List<MapModel>();
		private MapModel _socket;
		private MapModel _ball;

		public SupportPillar( MapElement owner )
		{
			_owner = owner;
		}

		public void Update( Transform transform )
		{
			_ball ??= _owner.AddModel( "models/surf_pillars/surf_pillar_ball_joint_02.vmdl" );
			_socket ??= _owner.AddModel( "models/surf_pillars/surf_pillar_ball_joint_01.vmdl" );

			const float pillarHeight = 512f;
			const float pillarOffset = 200f;

			var pillarTop = transform.Position - transform.Rotation.Up * pillarOffset;
			var pillarCount = (int)MathF.Ceiling( pillarTop.z / pillarHeight );

			while ( _pillars.Count > pillarCount )
			{
				_pillars[^1].Delete();
				_pillars.RemoveAt( _pillars.Count - 1 );
			}

			while ( _pillars.Count < pillarCount )
			{
				_pillars.Add( _owner.AddModel( "models/surf_pillars/surf_pillar_connecting_piece_tall.vmdl" ) );
			}

			for ( var i = 0; i < _pillars.Count; i++ )
			{
				_pillars[i].Transform = new Transform( pillarTop.WithZ( pillarTop.z - (i + 1) * pillarHeight ) );
			}

			_socket.Transform = new Transform( pillarTop );
			_ball.Transform = new Transform( pillarTop + Vector3.Up * 32f, transform.Rotation );
		}
	}

	public class MapElement : IValid
	{
		public int Id { get; init; }
		public bool IsValid { get; private set; } = true;

		public SurfMap Map { get; init; }
		public SceneWorld SceneWorld => Map?.SceneWorld;
		public PhysicsWorld PhysicsWorld => Map?.PhysicsWorld;
		public PhysicsBody PhysicsBody => Map?.PhysicsBody;

		public bool IsInGame => PhysicsWorld != null;

		private readonly List<SceneObject> _sceneObjects = new List<SceneObject>();
		private readonly List<Entity> _entities = new List<Entity>();
		private readonly List<PhysicsShape> _physicsShapes = new List<PhysicsShape>();

		public void Created()
		{
			OnCreated();
		}

		protected virtual void OnCreated()
		{

		}

		public void Changed()
		{
			Map._changedElements.Add( this );
			OnChanged();
		}

		protected virtual void OnChanged()
		{

		}

		public void Update()
		{
			OnUpdate();
		}

		protected virtual void OnUpdate()
		{

		}

		public void Removed()
		{
			if ( !IsValid )
			{
				return;
			}

			IsValid = false;

			foreach ( var entity in _entities )
			{
				entity.Delete();
			}

			_entities.Clear();

			foreach ( var sceneObj in _sceneObjects )
			{
				sceneObj.Delete();
			}

			_sceneObjects.Clear();

			foreach ( var shape in _physicsShapes )
			{
				shape.Remove();
			}

			_physicsShapes.Clear();

			OnRemoved();
		}

		protected virtual void OnRemoved()
		{

		}

		protected ModelEntity AddModelEntity( string modelPath )
		{
			var ent = new ModelEntity( modelPath );
			_entities.Add( ent );

			ent.SetupPhysicsFromModel( PhysicsMotionType.Static );

			return ent;
		}

		protected SceneObject AddSceneObject( Model model )
		{
			var obj = new SceneObject( SceneWorld, model );
			_sceneObjects.Add( obj );

			return obj;
		}

		protected PhysicsShape AddMeshShape( List<Vector3> vertices, List<int> indices )
		{
			var shape = PhysicsBody.AddMeshShape( vertices, indices );
			_physicsShapes.Add( shape );

			return shape;
		}

		public MapModel AddModel( string path )
		{
			if ( IsInGame )
			{
				return new MapModel( AddModelEntity( path ) );
			}

			return new MapModel( AddSceneObject( Model.Load( path ) ) );
		}

		protected void UpdateTransform( Transform transform )
		{
			foreach ( var sceneObject in _sceneObjects )
			{
				sceneObject.Transform = transform;
			}

			foreach ( var entity in _entities )
			{
				entity.Transform = transform;
			}
		}
	}

	public interface IPositionElement : IMapElement
	{
		Vector3 Position { get; set; }

		IMapElement Clone( Vector3 direction );
	}

	public interface IAnglesElement : IPositionElement
	{
		Angles Angles { get; set; }
	}

	public class SupportBracket : MapElement, IAnglesElement
	{
		public static Rotation RotationFromAngles( Angles angles )
		{
			return Rotation.From( angles.pitch, angles.yaw, 0f ) * Rotation.FromRoll( angles.roll );
		}

		public Vector3 Position { get; set; } = new Vector3( 0f, 0f, 512f );

		public Angles Angles { get; set; }

		public Rotation Rotation => RotationFromAngles( Angles );

		public List<BracketAttachment> Attachments { get; } = new List<BracketAttachment>();
		private readonly SupportPillar _pillar;

		public SupportBracket()
		{
			_pillar = new SupportPillar( this );
		}

		protected override void OnChanged()
		{
			foreach ( var attachment in Attachments )
			{
				attachment.Changed();
			}
		}

		protected override void OnUpdate()
		{
			_pillar.Update( new Transform( Position, Rotation ) );
		}

		protected override void OnRemoved()
		{
			foreach ( var attachment in Attachments.ToArray() )
			{
				Map.Remove( attachment );
			}
		}

		public IMapElement Clone( Vector3 direction )
		{
			var clone = Map.AddSupportBracket();
			var cloneForward = Vector3.Dot( Rotation.Forward, direction ) >= 0f;

			clone.Position = Position;
			clone.Angles = Angles;

			foreach ( var attachment in Attachments.ToArray() )
			{
				attachment.Bracket = clone;

				var attachmentClone = Map.AddBracketAttachment( this );

				attachmentClone.Min = attachment.Min;
				attachmentClone.Max = attachment.Max;
				attachmentClone.TangentScale = attachment.TangentScale;

				var anyTrackForward = false;
				var anyTrackBackward = false;

				foreach ( var track in attachment.TrackSections.ToArray() )
				{
					if ( track.Start == attachment )
					{
						if ( !cloneForward ) continue;

						anyTrackForward = true;

						track.Start = attachmentClone;

						var trackClone =
							Map.AddTrackSection( attachment, attachmentClone );
						trackClone.Material = track.Material;
					}
					else
					{
						if ( cloneForward ) continue;

						anyTrackBackward = true;

						track.End = attachmentClone;

						var trackClone =
							Map.AddTrackSection( attachmentClone, attachment );
						trackClone.Material = track.Material;
					}
				}

				if ( cloneForward && !anyTrackForward )
				{
					var track = Map.AddTrackSection( attachment, attachmentClone );
					track.Material = attachment.TrackSections.FirstOrDefault()?.Material ?? Map.DefaultTrackMaterial;
				}
				else if ( !cloneForward && !anyTrackBackward )
				{
					var track = Map.AddTrackSection( attachmentClone, attachment );
					track.Material = attachment.TrackSections.FirstOrDefault()?.Material ?? Map.DefaultTrackMaterial;
				}
			}

			return clone;
		}
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

		protected override void OnChanged()
		{
			foreach ( var track in TrackSections )
			{
				track.Changed();
			}
		}

		protected override void OnRemoved()
		{
			foreach ( var track in TrackSections.ToArray() )
			{
				Map.Remove( track );
			}

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

		protected override void OnUpdate()
		{
			if ( Start.IsValid() && End.IsValid() )
			{
				UpdateModel();
			}
		}

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

	public partial class SpawnPlatform : MapElement, IPositionElement
	{
		public Vector3 Position { get; set; }
		public float Yaw { get; set; }
		public int Stage { get; set; } = 1;

		protected override void OnCreated()
		{
			AddModel( "models/surf/spawn_platform.vmdl" );
		}

		protected override void OnUpdate()
		{
			UpdateTransform( new Transform( Position, Rotation.FromYaw( Yaw ) ) );
		}

		public IMapElement Clone( Vector3 direction )
		{
			var clone = Map.AddSpawnPlatform();

			clone.Position = Position;
			clone.Yaw = Yaw;

			return clone;
		}
	}

	public partial class Checkpoint : MapElement, IPositionElement, IAnglesElement
	{
		public Vector3 Position { get; set; }
		public Angles Angles { get; set; }
		public int Stage { get; set; } = 1;

		protected override void OnCreated()
		{
			AddModel( "models/surf/checkpoint.vmdl" );
		}

		protected override void OnUpdate()
		{
			UpdateTransform( new Transform( Position, Rotation.From( Angles ) ) );
		}

		public IMapElement Clone( Vector3 direction )
		{
			var clone = Map.AddCheckpoint();

			clone.Position = Position;
			clone.Angles = Angles;

			return clone;
		}
	}

	private int _nextElementId;

	private readonly Dictionary<int, MapElement> _elements = new();
	private readonly HashSet<MapElement> _changedElements = new();

	public SceneWorld SceneWorld { get; }
	public PhysicsWorld PhysicsWorld => PhysicsBody?.World;
	public PhysicsBody PhysicsBody { get; }

	public Material DefaultTrackMaterial { get; set; } = Material.Load( "materials/surf/track_default.vmat" );

	public SurfMap( SceneWorld sceneWorld, PhysicsBody physicsBody = null )
	{
		SceneWorld = sceneWorld;
		PhysicsBody = physicsBody;
	}

	public IEnumerable<MapElement> Elements => _elements.Values;

	public IEnumerable<SupportBracket> SupportBrackets => Elements.OfType<SupportBracket>();

	public IEnumerable<BracketAttachment> BracketAttachments => Elements.OfType<BracketAttachment>();

	public IEnumerable<TrackSection> TrackSections => Elements.OfType<TrackSection>();

	public IEnumerable<SpawnPlatform> SpawnPlatforms => Elements.OfType<SpawnPlatform>();

	public IEnumerable<Checkpoint> Checkpoints => Elements.OfType<Checkpoint>();

	private T AddElement<T>()
		where T : MapElement, new()
	{
		var elem = new T { Id = _nextElementId++, Map = this };
		_elements.Add( elem.Id, elem );

		elem.Created();
		elem.Changed();

		return elem;
	}

	public SupportBracket AddSupportBracket()
	{
		return AddElement<SupportBracket>();
	}

	public BracketAttachment AddBracketAttachment( SupportBracket bracket )
	{
		var attachment = AddElement<BracketAttachment>();

		attachment.Bracket = bracket;

		return attachment;
	}

	public TrackSection AddTrackSection( BracketAttachment start, BracketAttachment end )
	{
		var track = AddElement<TrackSection>();

		track.Start = start;
		track.End = end;

		return track;
	}

	public SpawnPlatform AddSpawnPlatform()
	{
		return AddElement<SpawnPlatform>();
	}

	public Checkpoint AddCheckpoint()
	{
		return AddElement<Checkpoint>();
	}

	public void Remove<T>( T element )
		where T : MapElement
	{
		Assert.AreEqual( _elements[element.Id], element );

		_elements.Remove( element.Id );
		element.Removed();
	}

	public void Clear()
	{
		foreach ( var elem in Elements.ToArray() )
		{
			if ( elem.IsValid )
			{
				Remove( elem );
			}
		}

		_nextElementId = 0;
		_elements.Clear();
	}

	public void Load( SurfMapAsset asset )
	{
		Clear();

		if ( asset == null )
		{
			return;
		}

		if ( asset.IsUninitialized )
		{
			var supportA = AddSupportBracket();
			var supportB = AddSupportBracket();

			supportA.Angles = new Angles( 0f, 0f, 50f );
			supportA.Position = new Vector3( 512f, 0f, 1024f );
			supportB.Angles = new Angles( 0f, 0f, 50f );
			supportB.Position = new Vector3( 2048f, 0f, 1024f );

			var attachA = AddBracketAttachment( supportA );
			var attachB = AddBracketAttachment( supportB );

			var track = AddTrackSection( attachA, attachB );

			track.Material = DefaultTrackMaterial;

			AddSpawnPlatform().Position = new Vector3( 0f, 0f, 1024f + 512f );
			AddCheckpoint().Position = new Vector3( 2048f + 512f, 0f, 768f + 512f );
		}
		else
		{
			foreach ( var bracket in asset.SupportBrackets )
			{
				_elements.Add( bracket.Id,
					new SupportBracket
					{
						Id = bracket.Id,
						Map = this,
						Position = bracket.Position,
						Angles = bracket.Angles,
					} );
			}

			foreach ( var attachment in asset.BracketAttachments )
			{
				_elements.Add( attachment.Id,
					new BracketAttachment
					{
						Id = attachment.Id,
						Map = this,
						Bracket = _elements[attachment.BracketId] as SupportBracket,
						TangentScale = attachment.TangentScale,
						Min = attachment.Min,
						Max = attachment.Max
					} );
			}

			foreach ( var track in asset.TrackSections )
			{
				_elements.Add( track.Id,
					new TrackSection
					{
						Id = track.Id,
						Map = this,
						Start = _elements[track.StartId] as BracketAttachment,
						End = _elements[track.EndId] as BracketAttachment,
						Material = track.Material
					} );
			}

			foreach ( var spawnPlatform in asset.SpawnPlatforms )
			{
				_elements.Add( spawnPlatform.Id,
					new SpawnPlatform
					{
						Id = spawnPlatform.Id,
						Map = this,
						Stage = spawnPlatform.Stage,
						Position = spawnPlatform.Position,
						Yaw = spawnPlatform.Yaw
					} );
			}

			foreach ( var checkpoint in asset.Checkpoints )
			{
				_elements.Add( checkpoint.Id,
					new Checkpoint
					{
						Id = checkpoint.Id,
						Map = this,
						Stage = checkpoint.Stage,
						Position = checkpoint.Position,
						Angles = checkpoint.Angles
					} );
			}

			_nextElementId = _elements.Count == 0 ? 0 : _elements.Max( x => x.Key ) + 1;

			foreach ( var elem in Elements )
			{
				elem.Created();
			}
		}

		foreach ( var elem in Elements )
		{
			elem.Changed();
		}

		UpdateChangedElements();
	}

	public bool UpdateChangedElements()
	{
		if ( _changedElements.Count == 0 )
		{
			return false;
		}

		var changed = _changedElements.ToArray();
		_changedElements.Clear();

		foreach ( var elem in changed )
		{
			elem.Update();
		}

		return true;
	}

	public void Save( SurfMapAsset asset )
	{
		asset.SupportBrackets = SupportBrackets.Select( x => new SurfMapAsset.SupportBracket
		{
			Id = x.Id,
			Position = x.Position,
			Angles = x.Angles,
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

		asset.SpawnPlatforms = SpawnPlatforms.Select( x => new SurfMapAsset.SpawnPlatform
		{
			Id = x.Id, Stage = x.Stage, Position = x.Position, Yaw = x.Yaw
		} ).ToList();

		asset.Checkpoints = Checkpoints.Select( x => new SurfMapAsset.Checkpoint
		{
			Id = x.Id, Stage = x.Stage, Position = x.Position, Angles = x.Angles
		} ).ToList();
	}
}
