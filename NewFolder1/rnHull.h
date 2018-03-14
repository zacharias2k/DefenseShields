//--------------------------------------------------------------------------------------------------
/**
	@file		rnHull.h

	@author		Dirk Gregorius
	@version	0.1
	@date		07/01/2012

	Copyright(C) 2012 by D. Gregorius. All rights reserved.
*/
//--------------------------------------------------------------------------------------------------
#pragma once

#include "Common/rnTypes.h"
#include "Common/rnMath.h"
#include "Common/rnGeometry.h"


//--------------------------------------------------------------------------------------------------
// rnHull
//--------------------------------------------------------------------------------------------------
struct rnHalfEdge
	{
	uint8 Next;
	uint8 Twin;
	uint8 Origin;
	uint8 Face;
	};

struct rnFace
	{
	uint8 Edge;
	};

struct rnHull
	{
	rnVector3 Centroid;
	int32 VertexCount;
	rnVector3* Vertices;
	int32 EdgeCount;
	rnHalfEdge* Edges;
	int32 FaceCount;
	rnFace* Faces;
	rnPlane* Planes;
	
	const rnVector3& GetVertex( int Index ) const;
	const rnHalfEdge* GetEdge( int Index ) const;
	const rnFace* GetFace( int Index ) const;
	const rnPlane& GetPlane( int Index ) const;
	rnVector3 GetSupport( const rnVector3& Direction ) const;
	
	int GetMemory( void ) const;
	};


#include "rnHull.inl"