using Editor;
using Editor.MapEditor;

namespace Sandbox.Surf.Editor;

public class Viewport : Frame
{
	public SurfMapEditor Editor { get; }
	public ViewportRendering Rendering { get; }

	public Viewport( SurfMapEditor editor ) : base( null )
	{
		Editor = editor;

		WindowTitle = "Viewport";
		Rendering = new ViewportRendering( editor.World );

		MouseTracking = true;

		Layout = Layout.Row();
		Layout.Add( Rendering, 1 );
	}

	[EditorEvent.Frame]
	private void Frame()
	{
		if ( !Editor.World.IsValid() )
		{
			return;
		}

		Editor.GizmoInstance.FirstPersonCamera( Rendering.Camera, Rendering );
		Editor.GizmoInstance.UpdateInputs( Rendering.Camera, Rendering );

		using ( Editor.GizmoInstance.Push() )
		{
			Gizmo.Draw.Color = new Color( 1f, 1f, 1f, 0.5f );
			Gizmo.Draw.LineBBox( new BBox( Vector3.Zero, 256f ) );
		}
	}
}

