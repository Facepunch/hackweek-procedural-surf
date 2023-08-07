using System.Collections.Generic;
using Editor;

namespace Sandbox.Surf.Editor;

public class ViewportRendering : NativeRenderingWidget
{
	public List<SceneObject> OwnedObjects { get; } = new List<SceneObject>();

	public ViewportRendering( SceneWorld world ) : base( null )
	{
		MinimumSize = 300;

		Camera = new SceneCamera( "CsgEditorCamera" )
		{
			World = world,
			BackgroundColor = Color.Black
		};

		MouseTracking = true;
	}

	public override void PreFrame()
	{
		Camera.AmbientLightColor = "#111";

		Camera.AntiAliasing = true;
		Camera.EnablePostProcessing = false;

		foreach ( var obj in OwnedObjects )
		{
			if ( obj.IsValid() )
			{
				obj.RenderingEnabled = true;
			}

		}
	}

	public override void PostFrame()
	{
		foreach ( var obj in OwnedObjects )
		{
			if ( obj.IsValid() )
			{
				obj.RenderingEnabled = false;
			}
		}
	}
}
