//--------------------------------------------------------------------------------------------------
// rnHull.inl
//
// Copyright(C) 2012 by D. Gregorius. All rights reserved.
//--------------------------------------------------------------------------------------------------


//--------------------------------------------------------------------------------------------------
// rnHull
//--------------------------------------------------------------------------------------------------
inline const rnVector3& rnHull::GetVertex( int Index ) const
	{
	return Vertices[ Index ];  
	}


//--------------------------------------------------------------------------------------------------
inline const rnHalfEdge* rnHull::GetEdge( int Index ) const
	{
	return Edges + Index;
	}


//--------------------------------------------------------------------------------------------------
inline const rnFace* rnHull::GetFace( int Index ) const
	{
	return Faces + Index;
	}


//--------------------------------------------------------------------------------------------------
inline const rnPlane& rnHull::GetPlane( int Index ) const
	{
	return Planes[ Index ];
	}


//--------------------------------------------------------------------------------------------------
inline rnVector3 rnHull::GetSupport( const rnVector3& Direction ) const
	{
	int MaxIndex = -1;
	float MaxProjection = -RN_F32_MAX;

	for ( int Index = 0; Index < VertexCount; ++Index )
		{
		float Projection = rnDot( Direction, Vertices[ Index ] );
		if ( Projection > MaxProjection )
			{
			MaxIndex = Index;
			MaxProjection = Projection;
			}
		}

	return Vertices[ MaxIndex ];
	}


//--------------------------------------------------------------------------------------------------
inline int rnHull::GetMemory( void ) const
	{
	int Memory = 0;
	Memory += sizeof( rnHull );
	Memory += VertexCount * sizeof( rnVector3 );
	Memory += EdgeCount * sizeof( rnHalfEdge );
	Memory += FaceCount * sizeof( rnFace );
	Memory += FaceCount * sizeof( rnPlane );

	return Memory;
	}


