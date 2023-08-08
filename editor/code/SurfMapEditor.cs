using System.Collections;
using System.Collections.Generic;
using System.IO;
using Editor;
using Sandbox.Diagnostics;

namespace Sandbox.Surf.Editor;

[EditorForAssetType( "surf" )]
public class SurfMapEditor : DockWindow, IAssetEditor
{
	public bool CanOpenMultipleAssets => false;

	public bool HasUnsavedChanges { get; private set; }

	public Asset Asset { get; private set; }
	public SurfMapAsset Target { get; private set; }
	public SurfMap Map { get; }
	public SceneWorld World { get; }
	public Gizmo.Instance GizmoInstance { get; }

	public SurfMapEditor()
	{
		DeleteOnClose = true;
		Size = new Vector2( 1280, 720 );
		MinimumWidth = 300;

		GizmoInstance = new Gizmo.Instance();
		World = GizmoInstance.World;

		Map = new SurfMap( World );

		SetupWorld();
	}

	public void AssetOpen( Asset asset )
	{
		Asset = asset;

		LoadFrom( Asset.LoadResource<SurfMapAsset>() );
	}

	private void LoadFrom( SurfMapAsset target )
	{
		Target = target;
		Map.Load( target );

		SetWindowIcon( Asset.AssetType.Icon128 );

		UpdateWindowTitle();
		RebuildUI();

		Show();
	}

	private void UpdateWindowTitle()
	{
		WindowTitle = $"{Asset.Name}{(HasUnsavedChanges ? "*" : "")} - Surf Map Editor";
	}

	private void SetupWorld()
	{
		_ = new SceneSunLight( World, Rotation.From( 60f, 30f, 0f ), Color.White * 6.0f + Color.Cyan * 1.0f )
		{
			ShadowsEnabled = true,
			SkyColor = Color.White * 0.15f + Color.Cyan * 0.25f
		};

		_ = new SceneSkyBox( World, Material.Load( "materials/skybox/light_test_default.vmat" ) );
		_ = new SceneCubemap( World, Texture.Load( "textures/cubemaps/default.vtex" ), BBox.FromPositionAndSize( Vector3.Zero, 1000 ) );
		var plane = new GridPlane( World );

		plane.FadeRadius = 8192f;
		plane.GridSize = 64f;
	}

	protected override void RestoreDefaultDockLayout()
	{
		base.RestoreDefaultDockLayout();

		RebuildUI();
	}

	public void MarkChanged( SurfMap.SupportBracket bracket )
	{
		MarkChanged();

		foreach ( var attachment in bracket.Attachments )
		{
			MarkChanged( attachment );
		}
	}

	public void MarkChanged( SurfMap.BracketAttachment attachment )
	{
		MarkChanged();

		foreach ( var track in attachment.TrackSections )
		{
			MarkChanged( track );
		}
	}

	public void MarkChanged( SurfMap.TrackSection track )
	{
		MarkChanged();

		track.UpdateModel();
	}

	public void MarkChanged()
	{
		if ( HasUnsavedChanges ) return;

		HasUnsavedChanges = true;
		UpdateWindowTitle();
	}

	public void RebuildUI()
	{
		DockManager.Clear();
		DockManager.RegisterDockType( "Viewport", "videocam", () => new Viewport( this ) );

		MenuBar.Clear();

		{
			var file = MenuBar.AddMenu( "File" );
			file.AddOption( new Option( "Save" ) { Shortcut = "Ctrl+S", Triggered = Save } );
			file.AddSeparator();
			file.AddOption( new Option( "Exit" ) { Triggered = Close } );

			var view = MenuBar.AddMenu( "View" );
			view.AboutToShow += () => CreateDynamicViewMenu( view );
		}

		var vp = new Viewport( this );
		DockManager.AddDock( null, vp );
	}

	private void Save()
	{
		Map.Save( Target );

		Asset.SaveToMemory( Target );
		Asset.SaveToDisk( Target );

		HasUnsavedChanges = false;
		UpdateWindowTitle();
	}
}
