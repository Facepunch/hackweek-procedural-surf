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

		Editor.GizmoInstance.FirstPersonCamera( Rendering.Camera, Rendering );
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
								if ( Gizmo.Control.Arrow( "Tangent", node.Rotation.Forward, out var tangent, node.Tangent, girth: 32f ) )
								{
									node.Tangent += tangent;
									ramp.Nodes[j] = node;
								}
							}
							else if ( Rotating )
							{
								if ( Gizmo.Control.Rotate( "Rotation", node.Rotation, out var newRot ) )
								{
									node.Rotation = newRot;
									ramp.Nodes[j] = node;
								}
							}
							else
							{
								if ( Gizmo.Control.Position( "Position", node.Position, out var newPos, node.Rotation ) )
								{
									node.Position = newPos;
									ramp.Nodes[j] = node;
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

						var up = node.Rotation.Up;
						var right = node.Rotation.Right;

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
}
