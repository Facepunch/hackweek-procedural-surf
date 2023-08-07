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

			if ( Editor.Target?.Ramps == null )
			{
				return;
			}

			for ( var i = 0; i < Editor.Target.Ramps.Count; ++i )
			{
				var ramp = Editor.Target.Ramps[i];

				if ( ramp.Nodes == null )
				{
					ramp.Nodes = new List<SurfMap.Node>();
				}

				using ( Gizmo.Scope( $"Ramp{i}" ) )
				{
					for ( var j = 0; j < ramp.Nodes.Count; ++j)
					{
						var node = ramp.Nodes[j];

						using ( Gizmo.Scope( $"Node{j}", node.Position ) )
						{
							if ( Scaling )
							{
								using ( Gizmo.GizmoControls.PushFixedScale() )
								{
									var forward = Rotation.From( node.Pitch, node.Yaw, 0f ).Forward;

									if ( Gizmo.Control.Arrow( "Tangent", forward, out var tangent ) )
									{
										node.Tangent += tangent;
										ramp.Nodes[j] = node;
										Editor.MarkChanged();
									}
								}
							}
							else if ( Rotating )
							{
								if ( Gizmo.Control.Rotate( "Rotation", new Angles( node.Pitch, node.Yaw, 0f ), out var newAngles ) )
								{
									node.Pitch = Math.Clamp( newAngles.pitch, -80f, 80f );
									node.Yaw = newAngles.yaw;
									ramp.Nodes[j] = node;
									Editor.MarkChanged();
								}
							}
							else
							{
								if ( Gizmo.Control.Position( "Position", node.Position, out var newPos, Rotation.From( node.Pitch, node.Yaw, 0f ) ) )
								{
									node.Position = newPos;
									ramp.Nodes[j] = node;
									Editor.MarkChanged();
								}
							}
						}
					}

					for ( var j = 0; j <= (ramp.Nodes.Count - 1) * 16; ++j )
					{
						var index = j / 16f;

						var prev = ramp.Nodes[Math.Clamp( (int)MathF.Floor( index ), 0, ramp.Nodes.Count - 1 )];
						var next = ramp.Nodes[Math.Clamp( (int)MathF.Ceiling( index ), 0, ramp.Nodes.Count - 1 )];

						var t = index - MathF.Floor( index );

						var node = Node.CubicBeizer( in prev, in next, t );

						var rotation = Rotation.From( node.Pitch, node.Yaw, 0f );
						var forward = rotation.Forward;
						var right = Vector3.Cross( forward, new Vector3( 0f, 0f, 1f ) ).Normal;
						var up = Vector3.Cross( right, forward ).Normal;

						var top = node.Position;
						var bl = top - up * node.Height - right * node.Width * 0.5f;
						var br = bl + right * node.Width;

						Gizmo.Draw.Line( top, bl );
						Gizmo.Draw.Line( top, br );
					}
				}
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
