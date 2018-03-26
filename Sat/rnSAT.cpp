//--------------------------------------------------------------------------------------------------
// rnSAT.cpp
//
// Copyright(C) 2012 by D. Gregorius. All rights reserved.
//--------------------------------------------------------------------------------------------------
#include "rnSAT.h"

#include "Common/rnTypes.h"
#include "Common/rnMath.h"
#include "Common/rnGeometry.h"


//--------------------------------------------------------------------------------------------------
// Face queries
//--------------------------------------------------------------------------------------------------
void rnQueryFaceDirections( rnFaceQuery& Out, const rnTransform& Transform1, const rnHull* Hull1, const rnTransform& Transform2, const rnHull* Hull2 )
	{
	// We perform all computations in local space of the second hull
	rnTransform Transform = rnTMul( Transform2, Transform1 );

	int MaxIndex = -1;
	float MaxSeparation = -RN_F32_MAX;

	for ( int Index = 0; Index < Hull1->FaceCount; ++Index )
		{
		rnPlane Plane = Transform * Hull1->GetPlane( Index );
		
		float Separation = rnProject( Plane, Hull2 );
		if ( Separation > MaxSeparation )
			{
			MaxIndex = Index;
			MaxSeparation = Separation;
			}
		}

	Out.Index = MaxIndex;
	Out.Separation = MaxSeparation;
	}


//--------------------------------------------------------------------------------------------------
float rnProject( const rnPlane& Plane, const rnHull* Hull )
	{
	rnVector3 Support = Hull->GetSupport( -Plane.Normal );
	return rnDistance( Plane, Support );
	}


//--------------------------------------------------------------------------------------------------
// Edge queries
//--------------------------------------------------------------------------------------------------
static RN_FORCEINLINE bool rnIsMinkowskiFace( const rnVector3& A, const rnVector3& B, const rnVector3& B_x_A, const rnVector3& C, const rnVector3& D, const rnVector3& D_x_C )
	{
	// Test if arcs AB and CD intersect on the unit sphere 
	float CBA = rnDot( C, B_x_A );
	float DBA = rnDot( D, B_x_A );
	float ADC = rnDot( A, D_x_C );
	float BDC = rnDot( B, D_x_C );

	return CBA * DBA < 0.0f && ADC * BDC < 0.0f && CBA * BDC > 0.0f;
	}



//--------------------------------------------------------------------------------------------------
void rnQueryEdgeDirections( rnEdgeQuery& Out, const rnTransform& Transform1, const rnHull* Hull1, const rnTransform& Transform2, const rnHull* Hull2 )
	{
	// We perform all computations in local space of the second hull
	rnTransform Transform = rnTMul( Transform2, Transform1 );

	// Transform reference center of the first hull into local space of the second hull
	rnVector3 C1 = Transform * Hull1->Centroid;

	// Find axis of minimum penetration
	int MaxIndex1 = -1;
	int MaxIndex2 = -1;
	float MaxSeparation = -RN_F32_MAX;

	for ( int Index1 = 0; Index1 < Hull1->EdgeCount; Index1 += 2 )
		{
		const rnHalfEdge* Edge1 = Hull1->GetEdge( Index1 );
		const rnHalfEdge* Twin1 = Hull1->GetEdge( Index1 + 1 );
		RN_ASSERT( Edge1->Twin == Index1 + 1 && Twin1->Twin == Index1 );

		rnVector3 P1 = Transform * Hull1->Vertices[ Edge1->Origin ];
		rnVector3 Q1 = Transform * Hull1->Vertices[ Twin1->Origin ];
		rnVector3 E1 = Q1 - P1;

		rnVector3 U1 = Transform.Rotation * Hull1->Planes[ Edge1->Face ].Normal;
		rnVector3 V1 = Transform.Rotation * Hull1->Planes[ Twin1->Face ].Normal;

		for ( int Index2 = 0; Index2 < Hull2->EdgeCount; Index2 += 2 )
			{
			const rnHalfEdge* Edge2 = Hull2->GetEdge( Index2 );
			const rnHalfEdge* Twin2 = Hull2->GetEdge( Index2 + 1 );
			RN_ASSERT( Edge2->Twin == Index2 + 1 && Twin2->Twin == Index2 );

			rnVector3 P2 = Hull2->Vertices[ Edge2->Origin ];
			rnVector3 Q2 = Hull2->Vertices[ Twin2->Origin ];
			rnVector3 E2 = Q2 - P2;

			rnVector3 U2 = Hull2->Planes[ Edge2->Face ].Normal;
			rnVector3 V2 = Hull2->Planes[ Twin2->Face ].Normal;

			if ( rnIsMinkowskiFace( U1, V1, -E1, -U2, -V2, -E2 ) )
				{
				float Separation = rnProject( P1, E1, P2, E2, C1 );
				if ( Separation > MaxSeparation )
					{
					MaxIndex1 = Index1;
					MaxIndex2 = Index2;
					MaxSeparation = Separation;
					}
				}
			}
		}

	Out.Index1 = MaxIndex1;
	Out.Index2 = MaxIndex2;
	Out.Separation = MaxSeparation;
	}


//--------------------------------------------------------------------------------------------------
float rnProject( const rnVector3& P1, const rnVector3& E1, const rnVector3& P2, const rnVector3& E2, const rnVector3& C1 )
	{
	// Build search direction
	rnVector3 E1_x_E2 = rnCross( E1, E2 );

	// Skip near parallel edges: |e1 x e2| = sin(alpha) * |e1| * |e2|
	const float kTolerance = 0.005f;
	
	float L = rnLength( E1_x_E2 );
	if ( L < kTolerance * rnSqrt( rnLengthSq( E1 ) * rnLengthSq( E2 ) ) )
		{
		return -RN_F32_MAX;
		}

	// Assure consistent normal orientation (here: Hull1 -> Hull2)
	rnVector3 N = E1_x_E2 / L;
	if ( rnDot ( N, P1 - C1 ) < 0.0f )
		{
		N = -N;
		}

	// s = Dot(n, p2) - d = Dot(n, p2) - Dot(n, p1) = Dot(n, p2 - p1) 
	return rnDot( N, P2 - P1 );
	}


