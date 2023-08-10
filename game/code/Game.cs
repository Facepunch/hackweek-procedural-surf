using Sandbox;
using Sandbox.UI.Construct;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Sandbox.Surf;

//
// You don't need to put things in a namespace, but it doesn't hurt.
//
namespace Sandbox;

/// <summary>
/// This is your game class. This is an entity that is created serverside when
/// the game starts, and is replicated to the client. 
/// 
/// You can use this to create things like HUDs and declare which player class
/// to use for spawned players.
/// </summary>
public partial class SurfGame : GameManager
{
	public new static SurfGame Current => GameManager.Current as SurfGame;

	[Net]
	public SurfMapAsset MapAsset { get; set; }

	public SurfMap Map { get; set; }

	private SurfMapAsset _loadedMapAsset;
	private int _loadedMapChangeIndex;

	[GameEvent.Entity.PostSpawn]
	private void ServerPostSpawn()
	{
		MapAsset = ResourceLibrary.Get<SurfMapAsset>( "maps/testing.surf" );
	}

	[GameEvent.Tick.Server]
	private void ServerTick()
	{
		SharedTick();
	}

	[GameEvent.Tick.Client]
	private void ClientTick()
	{
		SharedTick();
	}

	private void SharedTick()
	{
		if ( Map == null && MapAsset != null )
		{
			Map = new SurfMap( Game.SceneWorld, Game.PhysicsWorld.Body );
		}

		if ( Map != null )
		{
			var changeIndex = MapAsset?.ChangeIndex ?? 0;

			if ( _loadedMapAsset != MapAsset || _loadedMapChangeIndex != changeIndex )
			{
				_loadedMapAsset = MapAsset;
				_loadedMapChangeIndex = changeIndex;

				Map.Load( MapAsset );
			}

			Map.UpdateChangedElements();
		}
	}

	/// <summary>
	/// A client has joined the server. Make them a pawn to play with
	/// </summary>
	public override void ClientJoined( IClient client )
	{
		base.ClientJoined( client );

		// Create a pawn for this client to play with
		var pawn = new Surfer();
		client.Pawn = pawn;
		pawn.Respawn();
	}
}
