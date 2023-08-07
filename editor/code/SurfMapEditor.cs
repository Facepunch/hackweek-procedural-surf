using System.Collections.Generic;
using Editor;

namespace Sandbox.Surf.Editor;

[EditorForAssetType( "surf" )]
public class SurfMapEditor : DockWindow, IAssetEditor
{
	public bool CanOpenMultipleAssets => false;

	public bool HasUnsavedChanges { get; private set; }

	public Asset Asset { get; private set; }
	public SurfMap Target { get; private set; }
	public SceneWorld World { get; }
	public Gizmo.Instance GizmoInstance { get; }

	public SurfMapEditor()
	{
		DeleteOnClose = true;
		Size = new Vector2( 1280, 720 );
		MinimumWidth = 300;

		GizmoInstance = new Gizmo.Instance();
		World = GizmoInstance.World;

		SetupWorld();
	}

	public void AssetOpen( Asset asset )
	{
		Asset = asset;

		LoadFrom( Asset.LoadResource<SurfMap>() );
	}

	private void LoadFrom( SurfMap map )
	{
		Target = map;

		if ( Target.Ramps == null )
		{
			Target.Ramps = new List<SurfMap.Ramp>
			{
				new()
				{
					Nodes = new List<SurfMap.Node>
					{
						new()
						{
							Position = new Vector3( 0f, 0f, 0f ),
							Rotation = Rotation.Identity,
							Width = 384f,
							Height = 256f,
							Tangent = 128f
						},

						new()
						{
							Position = new Vector3( 1024f, 0f, 0f ),
							Rotation = Rotation.Identity,
							Width = 384f,
							Height = 256f,
							Tangent = 128f
						},

						new()
						{
							Position = new Vector3( 2048f, 0f, 0f ),
							Rotation = Rotation.Identity,
							Width = 384f,
							Height = 256f,
							Tangent = 128f
						},

						new()
						{
							Position = new Vector3( 1024f * 3f, 0f, 0f ),
							Rotation = Rotation.Identity,
							Width = 384f,
							Height = 256f,
							Tangent = 128f
						},

						new()
						{
							Position = new Vector3( 4096f, 0f, 0f ),
							Rotation = Rotation.Identity,
							Width = 384f,
							Height = 256f,
							Tangent = 128f
						}
					}
				}
			};
		}

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
		_ = new SceneSunLight( World, Rotation.From( -50f, 30f, 0f ), Color.White * 6.0f + Color.Cyan * 1.0f )
		{
			ShadowsEnabled = true,
			SkyColor = Color.White * 0.15f + Color.Cyan * 0.25f
		};

		_ = new SceneSkyBox( World, Material.Load( "materials/skybox/light_test_default.vmat" ) );
		_ = new SceneCubemap( World, Texture.Load( "textures/cubemaps/default.vtex" ), BBox.FromPositionAndSize( Vector3.Zero, 1000 ) );
		var plane = new GridPlane( World );

		plane.FadeRadius = 8192f;
		plane.GridSize = 128f;
	}

	protected override void RestoreDefaultDockLayout()
	{
		base.RestoreDefaultDockLayout();

		RebuildUI();
	}

	public void RebuildUI()
	{
		DockManager.Clear();
		DockManager.RegisterDockType( "Viewport", "videocam", () => new Viewport( this ) );

		MenuBar.Clear();

		{
			var file = MenuBar.AddMenu( "File" );
			file.AddOption( new Option( "Save" ) { Shortcut = "Ctrl+S", Triggered = () => { } } );
			file.AddSeparator();
			file.AddOption( new Option( "Exit" ) { Triggered = Close } );

			var view = MenuBar.AddMenu( "View" );
			view.AboutToShow += () => CreateDynamicViewMenu( view );
		}

		var vp = new Viewport( this );
		DockManager.AddDock( null, vp );
	}
}
