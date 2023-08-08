using System;
using System.Collections.Generic;
using Editor;
using Editor.MapEditor;
using static Sandbox.Surf.SurfMap;

namespace Sandbox.Surf.Editor;

public class Viewport : Frame
{
	public SurfMapEditor Editor { get; }
	public ViewportRendering Rendering { get; }

	public bool Rotating { get; set; }
	public bool Scaling { get; set; }

	public Viewport( SurfMapEditor editor ) : base( null )
	{
		Editor = editor;

		WindowTitle = "Viewport";
		Rendering = new ViewportRendering( editor.World );

		MouseTracking = true;

		Layout = Layout.Row();
		Layout.Add( Rendering, 1 );
	}

	protected override void OnKeyPress( KeyEvent e )
	{
		base.OnKeyPress( e );

		Rotating |= e.Key == KeyCode.R;
		Scaling |= e.Key == KeyCode.E;
	}

	protected override void OnKeyRelease( KeyEvent e )
	{
		base.OnKeyRelease( e );

		Rotating &= e.Key != KeyCode.R;
		Scaling &= e.Key != KeyCode.E;
	}

	[EditorEvent.Frame]
	private void Frame()
	{
		if ( !Editor.World.IsValid() )
		{
			return;
		}

		FirstPersonCamera( Editor.GizmoInstance, Rendering.Camera, Rendering );
		Editor.GizmoInstance.UpdateInputs( Rendering.Camera, Rendering );

		using ( Editor.GizmoInstance.Push() )
		{
			Gizmo.Draw.Color = new Color( 1f, 1f, 1f, 0.5f );

			foreach ( var bracket in Editor.Target.SupportBrackets )
			{
				Gizmo.Draw.Line( bracket.Position.WithZ( 0f ), bracket.Position );
			}
		}
	}

	public static void FirstPersonCamera( Gizmo.Instance self, SceneCamera camera, Widget canvas )
	{
		ArgumentNullException.ThrowIfNull( camera );
		ArgumentNullException.ThrowIfNull( canvas );

		var rightMouse = Application.MouseButtons.HasFlag( MouseButtons.Right );
		var middleMouse = Application.MouseButtons.HasFlag( MouseButtons.Middle );

		if ( (rightMouse || middleMouse) && canvas.IsUnderMouse )
		{
			canvas.Focus();

			var delta = (Application.CursorPosition - self.PreviousInput.CursorPosition) * 0.1f;

			// lock to the center of the screen
			Application.CursorPosition = canvas.ScreenPosition + canvas.Size * 0.5f;

			if ( self.ControlMode != "firstperson" )
			{
				delta = 0;
				self.ControlMode = "firstperson";
				self.StompCursorPosition( Application.CursorPosition );
				Application.AllowShortcuts = false;
			}

			var moveSpeed = 8f;

			if ( Application.KeyboardModifiers.HasFlag( KeyboardModifiers.Shift ) ) moveSpeed *= 6.0f;
			if ( Application.KeyboardModifiers.HasFlag( KeyboardModifiers.Alt ) ) moveSpeed /= 6.0f;

			if ( rightMouse )
			{
				var angles = camera.Angles;

				angles.roll = 0;
				angles.yaw -= delta.x;
				angles.pitch += delta.y;

				camera.Angles = angles;
			}
			else if ( middleMouse )
			{
				camera.Position += camera.Rotation.Right * delta.x * moveSpeed * 2f;
				camera.Position += camera.Rotation.Down * delta.y * moveSpeed * 2f;
			}

			var move = Vector3.Zero;

			if ( Application.IsKeyDown( KeyCode.W ) ) move += camera.Rotation.Forward;
			if ( Application.IsKeyDown( KeyCode.S ) ) move += camera.Rotation.Backward;
			if ( Application.IsKeyDown( KeyCode.A ) ) move += camera.Rotation.Left;
			if ( Application.IsKeyDown( KeyCode.D ) ) move += camera.Rotation.Right;

			move = move.Normal;

			camera.Position += move * RealTime.Delta * 100.0f * moveSpeed;
			canvas.Cursor = CursorShape.Blank;
		}
		else
		{
			canvas.Cursor = CursorShape.None;

			if ( self.ControlMode != "mouse" )
			{
				self.ControlMode = "mouse";
				Application.AllowShortcuts = true;
			}
		}
	}
}
