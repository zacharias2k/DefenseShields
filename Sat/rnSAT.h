//--------------------------------------------------------------------------------------------------
/**
	@file		rnSAT.h

	@author		Dirk Gregorius
	@version	0.1
	@date		07/01/2012

	Copyright(C) 2012 by D. Gregorius. All rights reserved.
*/
//--------------------------------------------------------------------------------------------------
#pragma once

#include "Common/rnTypes.h"
#include "Common/rnMath.h"
#include "Common/rnMemory.h"

#include "Collision/Shapes/rnHull.h"


//--------------------------------------------------------------------------------------------------
// Face queries
//--------------------------------------------------------------------------------------------------
struct rnFaceQuery
	{
	int Index;
	float Separation;
	};

void rnQueryFaceDirections( rnFaceQuery& Out, const rnTransform& Transform1, const rnHull* Hull1, const rnTransform& Transform2, const rnHull* Hull2 );
float rnProject( const rnPlane& Plane, const rnHull* Hull );


//--------------------------------------------------------------------------------------------------
// Edge queries
//--------------------------------------------------------------------------------------------------
struct rnEdgeQuery
	{
	int Index1;
	int Index2;
	float Separation;
	};

void rnQueryEdgeDirections( rnEdgeQuery& Out, const rnTransform& Transform1, const rnHull* Hull1, const rnTransform& Transform2, const rnHull* Hull2 );
float rnProject( const rnVector3& P1, const rnVector3& E1, const rnVector3& P2, const rnVector3& E2, const rnVector3& C1 );

