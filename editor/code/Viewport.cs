﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Editor;

namespace Sandbox.Surf.Editor;

public enum EditMode
{
	Move,
	Rotate
}

public class Viewport : Frame
{
	public SurfMapEditor Editor { get; }
	public ViewportRendering Rendering { get; }

	private bool _rotatePressed;

	private Vector3 _dragOffset;
	private float _rotateOffset;
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

	protected override void OnKeyPress( KeyEvent e )
	{
		base.OnKeyPress( e );

		_rotatePressed |= e.Key == KeyCode.R;
	}

	protected override void OnKeyRelease( KeyEvent e )
	{
		base.OnKeyRelease( e );

		_rotatePressed &= e.Key != KeyCode.R;
	}

	private readonly List<SurfMap.SupportBracket> _brackets = new List<SurfMap.SupportBracket>();

	[EditorEvent.Frame]
	private void Frame()
	{
		if ( !Editor.World.IsValid() )
		{
			return;
		}

		var mode = _rotatePressed ? EditMode.Rotate : EditMode.Move;
		var cloning = Application.KeyboardModifiers.HasFlag( KeyboardModifiers.Ctrl );

		FirstPersonCamera( Editor.GizmoInstance, Rendering.Camera, Rendering );
		Editor.GizmoInstance.UpdateInputs( Rendering.Camera, Rendering );

		const float gridSize = 64f;
		const float rotateSnapSize = 22.5f;

		using var instScope = Editor.GizmoInstance.Push();

		_brackets.Clear();
		_brackets.AddRange( Editor.Map.SupportBrackets );

		foreach ( var bracket in _brackets )
		{
			using var bracketBaseScope = Gizmo.Scope( $"Bracket{bracket.Id}", bracket.Position.WithZ( 0f ) );

			switch ( mode )
			{
				case EditMode.Move:
				{
					Gizmo.Draw.Color = Gizmo.Colors.Green;
					if ( Gizmo.Control.Arrow( "Height", Vector3.Up, out var dist, bracket.Position.z ) )
					{
						_dragOffset.z += dist;

						var snappedOffset = MathF.Round( _dragOffset.z / gridSize ) * gridSize;

						if ( snappedOffset != 0f )
						{
							_dragOffset.z -= snappedOffset;

							bracket.Position = bracket.Position.WithZ( bracket.Position.z + snappedOffset );
							Editor.MarkChanged( bracket );
						}
					}

					using var _ = Gizmo.GizmoControls.PushFixedScale( 1f );

					Gizmo.Draw.Color = Gizmo.Colors.Blue;
					if ( Gizmo.Control.DragSquare( "BasePos", new Vector2( 4f, 4f ), Rotation.FromPitch( 90f ), out var movement ) )
					{
						_dragOffset += movement.WithZ( 0f );

						var snappedOffset = _dragOffset.SnapToGrid( gridSize, true, true, false );

						if ( snappedOffset != 0f )
						{
							_dragOffset -= snappedOffset;

							if ( cloning && !_dragStarted )
							{
								var clone = Editor.Map.AddSupportBracket();
								var cloneForward = Vector3.Dot( bracket.Rotation.Forward, snappedOffset ) >= 0f;

								clone.Position = bracket.Position;
								clone.Yaw = bracket.Yaw;
								clone.Roll = bracket.Roll;

								foreach ( var attachment in bracket.Attachments.ToArray() )
								{
									attachment.Bracket = clone;

									var attachmentClone = Editor.Map.AddBracketAttachment( bracket );

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

											var trackClone = Editor.Map.AddTrackSection( attachment, attachmentClone );
											trackClone.Material = track.Material;
										}
										else
										{
											if ( cloneForward ) continue;

											anyTrackBackward = true;

											track.End = attachmentClone;

											var trackClone = Editor.Map.AddTrackSection( attachmentClone, attachment );
											trackClone.Material = track.Material;
										}
									}

									if ( cloneForward && !anyTrackForward )
									{
										var track = Editor.Map.AddTrackSection( attachment, attachmentClone );
										track.Material = attachment.TrackSections.FirstOrDefault()?.Material ?? Editor.Map.DefaultTrackMaterial;
									}
									else if ( !cloneForward && !anyTrackBackward )
									{
										var track = Editor.Map.AddTrackSection( attachmentClone, attachment );
										track.Material = attachment.TrackSections.FirstOrDefault()?.Material ?? Editor.Map.DefaultTrackMaterial;
									}
								}

								Editor.MarkChanged( clone );
							}

							_dragStarted = true;
							bracket.Position += snappedOffset;
							Editor.MarkChanged( bracket );
						}
					}
				}
				break;

				case EditMode.Rotate:
				{
					using ( Gizmo.GizmoControls.PushFixedScale( 1f ) )
					{
						if ( Gizmo.Control.RotateSingle( "Yaw", Rotation.FromPitch( 90f ), Gizmo.Colors.Blue, out var yawDelta ) )
						{
							_rotateOffset += yawDelta.Yaw();

							var snappedOffset = MathF.Round( _rotateOffset / rotateSnapSize ) * rotateSnapSize;

							if ( snappedOffset != 0f )
							{
								bracket.Yaw += snappedOffset;
								_rotateOffset -= snappedOffset;
								Editor.MarkChanged( bracket );
							}
						}
					}

					using var bracketTopScope = Gizmo.Scope( "Top", new Vector3( 0f, 0f, bracket.Position.z ), Rotation.FromYaw( bracket.Yaw ) );

					using ( Gizmo.GizmoControls.PushFixedScale( 1f ) )
					{
						if ( Gizmo.Control.RotateSingle( "Roll", Rotation.Identity, Gizmo.Colors.Red, out var rollDelta ) )
						{
							_rotateOffset += rollDelta.Roll();

							var snappedOffset = MathF.Round( _rotateOffset / 5f ) * 5f;

							if ( snappedOffset != 0f )
							{
								bracket.Roll = Math.Clamp( bracket.Roll + snappedOffset, -85f, 85f );
								_rotateOffset -= snappedOffset;
								Editor.MarkChanged( bracket );
							}
						}
					}
				}
				break;
			}
		}

		foreach ( var attachment in Editor.Map.BracketAttachments )
		{
			var bracket = attachment.Bracket;
			var tangent = Rotation.From( 0f, bracket.Yaw, bracket.Roll ).Right;

			Gizmo.Draw.Line( bracket.Position + tangent * attachment.Min, bracket.Position + tangent * attachment.Max );
		}

		if ( !Application.MouseButtons.HasFlag( MouseButtons.Left ) )
		{
			_dragOffset = 0f;
			_rotateOffset = 0f;
			_dragStarted = false;
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
