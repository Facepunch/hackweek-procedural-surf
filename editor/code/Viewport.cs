using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Editor;

namespace Sandbox.Surf.Editor;

public enum EditMode
{
	Move,
	Rotate,
	Delete
}

public class Viewport : Frame
{
	public SurfMapEditor Editor { get; }
	public ViewportRendering Rendering { get; }

	public float GridSize { get; } = 64f;

	private Vector3 _dragOffset;
	private Angles _rotateOffset;
	private bool _dragStarted;

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

		var mode = Application.IsKeyDown( KeyCode.R )
			? EditMode.Rotate
			: Application.IsKeyDown( KeyCode.Delete )
				? EditMode.Delete
				: EditMode.Move;

		FirstPersonCamera( Editor.GizmoInstance, Rendering.Camera, Rendering );
		Editor.GizmoInstance.UpdateInputs( Rendering.Camera, Rendering );

		using var instScope = Editor.GizmoInstance.Push();

		switch ( mode )
		{
			case EditMode.Move:
				MoveFrame();
				break;

			case EditMode.Delete:
				DeleteFrame();
				break;

			case EditMode.Rotate:
				RotateFrame();
				break;
		}

		foreach ( var attachment in Editor.Map.BracketAttachments )
		{
			var bracket = attachment.Bracket;
			var tangent = bracket.Rotation.Right;

			Gizmo.Draw.Line( bracket.Position + tangent * attachment.Min, bracket.Position + tangent * attachment.Max );
		}

		if ( Editor.Map.UpdateChangedElements() )
		{
			Editor.MarkChanged();
		}

		if ( !Application.MouseButtons.HasFlag( MouseButtons.Left ) )
		{
			_dragOffset = 0f;
			_rotateOffset = Angles.Zero;
			_dragStarted = false;
		}
	}

	private void MoveFrame()
	{
		var cloning = Application.KeyboardModifiers.HasFlag( KeyboardModifiers.Ctrl );
		var posElements = Editor.Map.Elements.OfType<SurfMap.IPositionElement>().ToArray();

		foreach ( var element in posElements )
		{
			using var elemScope = Gizmo.Scope( $"PosElem{element.Id}", element.Position.WithZ( 0f ) );

			Gizmo.Draw.IgnoreDepth = true;
			Gizmo.Draw.Color = Gizmo.Colors.Green;

			if ( Arrow( "Height", Vector3.Up, out var heightDelta, element.Position.z, girth: 64f, headLength: 48f ) )
			{
				_dragOffset.z += heightDelta;

				var snappedOffset = MathF.Round( _dragOffset.z / GridSize ) * GridSize;

				if ( snappedOffset != 0f )
				{
					_dragOffset.z -= snappedOffset;

					element.Position = element.Position.WithZ( Math.Max( element.Position.z + snappedOffset, 128f ) );
					element.Changed();
				}
			}

			{
				using var _ = Gizmo.GizmoControls.PushFixedScale( 1f );

				Gizmo.Draw.Color = Gizmo.Colors.Blue;
				if ( Gizmo.Control.DragSquare( "BasePos", new Vector2( 4f, 4f ), Rotation.FromPitch( 90f ), out var movement ) )
				{
					_dragOffset += movement.WithZ( 0f );

					var snappedOffset = _dragOffset.SnapToGrid( GridSize, true, true, false );

					if ( snappedOffset != 0f )
					{
						_dragOffset -= snappedOffset;

						if ( cloning && !_dragStarted )
						{
							var clone = element.Clone( snappedOffset );
							clone.Changed();
						}

						_dragStarted = true;
						element.Position += snappedOffset;
						element.Changed();
					}
				}
			}

			if ( Application.IsKeyDown( KeyCode.Delete ) && Gizmo.IsChildSelected )
			{
				Log.Info( $"Delete {element.Id}!" );
			}
		}

		var attachments = Editor.Map.Elements.OfType<SurfMap.BracketAttachment>().ToArray();

		foreach ( var attachment in attachments )
		{
			using var elemScope = Gizmo.Scope( $"Attachment{attachment.Id}", attachment.Bracket.Position, attachment.Bracket.Rotation );

			Gizmo.Draw.Color = Gizmo.Colors.Red;
			Gizmo.Draw.IgnoreDepth = true;

			if ( Arrow( "Tangent", Vector3.Forward, out var tangentDelta, 128f + attachment.TangentScale * 256f, girth: 64f, headLength: 48f ) )
			{
				attachment.TangentScale = 0.5f; // Math.Clamp( attachment.TangentScale + tangentDelta / 256f, 0f, 0.75f );
				attachment.Changed();
			}
		}
	}

	private void RotateFrame()
	{
		var anglesElements = Editor.Map.Elements.OfType<SurfMap.IAnglesElement>().ToArray();

		foreach ( var element in anglesElements )
		{
			using var elemScope = Gizmo.Scope( $"AnglesElem{element.Id}", element.Position );

			if ( Rotate( "Rotation", element.Angles, out var delta ) )
			{
				_rotateOffset += delta;

				var snappedRotation = new Angles( MathF.Round( _rotateOffset.pitch / 5f ) * 5f,
					MathF.Round( _rotateOffset.yaw / 22.5f ) * 22.5f,
					MathF.Round( _rotateOffset.roll / 5f ) * 5f );

				if ( snappedRotation != Angles.Zero )
				{
					_rotateOffset -= snappedRotation;

					var newRot = element.Angles + snappedRotation;

					newRot.pitch = Math.Clamp( newRot.pitch, -45f, 45f );
					newRot.roll = Math.Clamp( newRot.roll, -80f, 80f );

					element.Angles = newRot;
					element.Changed();
				}
			}
		}
	}

	private void DeleteFrame()
	{
		var posElements = Editor.Map.Elements.OfType<SurfMap.IPositionElement>().ToArray();

		foreach ( var element in posElements )
		{
			using var elemScope = Gizmo.Scope( $"PosElem{element.Id}", element.Position );

			Gizmo.Hitbox.Sphere( new Sphere( Vector3.Zero, 128f ) );

			Gizmo.Draw.Color = Gizmo.IsHovered ? Gizmo.Colors.Red : new Color( 1f, 1f, 1f, 0.75f );
			Gizmo.Draw.SolidSphere( Vector3.Zero, Gizmo.IsHovered ? 160f : 128f );

			if ( Gizmo.WasClicked )
			{
				Log.Info( $"Delete!" );
				Editor.Map.Remove( (SurfMap.MapElement) element );
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

	/// <summary>
	/// Draw an arrow - return move delta if interacted with
	/// </summary>
	public static bool Arrow( string name, Vector3 axis, out float distance, float length = 24.0f, float girth = 6.0f, float headLength = 4f, float axisOffset = 2.0f, float cullAngle = 10.0f, float snapSize = 0.0f, string head = "cone" )
	{
		distance = 0;

		var angle = Vector3.GetAngle( axis, Gizmo.Transform.NormalToLocal( Camera.Rotation.Forward ) );

		if ( angle < cullAngle || angle > 180.0f - cullAngle )
			return false;

		var localCam = Gizmo.Transform.RotationToLocal( Camera.Rotation );

		// Use the camera to provide a plane that'll work for us
		var rot = Rotation.LookAt( axis, Vector3.Up );

		using var x = Sandbox.Gizmo.Scope( name, axis * axisOffset, rot );

		girth *= 0.5f;

		Sandbox.Gizmo.Hitbox.BBox( new BBox( new Vector3( 0, -girth, -girth ), new Vector3( length, girth, girth ) ) );

		if ( !Sandbox.Gizmo.IsHovered ) Sandbox.Gizmo.Draw.Color = Sandbox.Gizmo.Draw.Color.Darken( 0.1f );

		Sandbox.Gizmo.Draw.LineThickness = 2f;


		var lineLength = length;

		if ( snapSize > 0 )
		{
			lineLength = lineLength.SnapToGrid( snapSize );
		}

		// not pressed, no movement
		Sandbox.Gizmo.Draw.Line( 0, Vector3.Forward * (lineLength - headLength) );

		if ( head == "cone" )
		{
			Sandbox.Gizmo.Draw.SolidCone( Vector3.Forward * (lineLength - headLength), Vector3.Forward * headLength, headLength * 0.33f );
		}

		if ( head == "box" )
		{
			Sandbox.Gizmo.Draw.SolidBox( new BBox( Vector3.Forward * (lineLength - headLength), headLength * 0.5f ) );
		}

		if ( !Sandbox.Gizmo.IsPressed )
			return false;

		// use a plane that follows the axis but that uses the camera's plane
		Gizmo.Transform = Gizmo.Transform.WithRotation( Rotation.LookAt( Gizmo.Transform.Rotation.Forward, Camera.Rotation.Forward ) );


		//
		// Get the delta between trace hits against a plane
		//
		var delta = Sandbox.Gizmo.GetMouseDelta( Vector3.Zero, Vector3.Up );

		distance = Vector3.Forward.Dot( delta );

		// restrict movement to the axis direction
		return distance != 0.0f;

	}

	public static bool Rotate( string name, Angles value, out Angles outDelta )
	{
		using ( Gizmo.GizmoControls.PushFixedScale() )
		{
			outDelta = default;
			var flag = false;

			Gizmo.Draw.IgnoreDepth = true;

			if ( Gizmo.Control.RotateSingle( "yaw", Rotation.LookAt( Vector3.Up ), Gizmo.Colors.Yaw, out var deltaYaw ) )
			{
				outDelta.yaw += deltaYaw.Yaw();
				flag = true;
			}

			using ( Gizmo.Scope( name, Vector3.Zero, Rotation.FromYaw( value.yaw ) ) )
			{
				if ( Gizmo.Control.RotateSingle( "pitch", Rotation.LookAt( Vector3.Right ), Gizmo.Colors.Pitch,
					    out var deltaPitch ) )
				{
					outDelta.pitch += deltaPitch.Pitch();
					flag = true;
				}

				using ( Gizmo.Scope( name, Vector3.Zero, Rotation.FromPitch( value.pitch ) ) )
				{
					if ( Gizmo.Control.RotateSingle( "roll", Rotation.LookAt( Vector3.Forward ), Gizmo.Colors.Roll, out var deltaRoll ) )
					{
						outDelta.roll += deltaRoll.Roll();
						flag = true;
					}
				}
			}
			return flag;
		}
	}
}
