using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sandbox.Surf.Editor;

public class GridPlane : SceneObject
{
	private Vector3 _fadeOrigin;
	private float _gridSize;
	private float _fadeRadius;

	public Vector3 FadeOrigin
	{
		get => _fadeOrigin;
		set
		{
			if ( _fadeOrigin.Equals( value ) ) return;

			_fadeOrigin = value;
			Attributes.Set( "FadeOrigin", value );
		}
	}

	public float FadeRadius
	{
		get => _fadeRadius;
		set
		{
			if ( _fadeRadius.Equals( value ) ) return;

			_fadeRadius = value;
			Attributes.Set( "FadeRadius", value );
		}
	}

	public float GridSize
	{
		get => _gridSize;
		set
		{
			if ( _gridSize.Equals( value ) ) return;

			_gridSize = value;
			Attributes.Set( "GridSize", value );
		}
	}

	public static (Vector3 TangentU, Vector3 TangentV) GetTangents( Vector3 normal )
	{
		var fudge = Math.Abs( normal.x ) <= 0.70710678f
			? new Vector3( 1f, 0f, 0f )
			: Math.Abs( normal.y ) <= 0.70710678f
				? new Vector3( 0f, 1f, 0f )
				: new Vector3( 0f, 0f, 1f );

		var u = Vector3.Cross( fudge, normal ).Normal;
		var v = Vector3.Cross( u, normal );

		return (u, v);
	}

	public Vector3 Normal
	{
		get => Rotation.Up;
		set
		{
			Rotation = Rotation.LookAt( GetTangents( value ).TangentU, value );
		}
	}

	public GridPlane( SceneWorld world )
		: base( world, "models/surf/editor/gridplane.vmdl" )
	{
		Flags.IsTranslucent = true;
		Flags.CastShadows = false;

		GridSize = 8f;
		FadeRadius = 256f;
	}
}
