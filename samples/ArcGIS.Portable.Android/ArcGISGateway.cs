﻿using System;
using ArcGIS.ServiceModel;
using ArcGIS.ServiceModel.Operation;
using System.Threading.Tasks;
using ArcGIS.ServiceModel.Common;

namespace ArcGIS.Portable.Android
{
	public class ArcGISGateway : PortalGateway
	{
		public ArcGISGateway(ISerializer serializer)
			: base(@"http://sampleserver3.arcgisonline.com/ArcGIS/", serializer)
		{ }

		public Task<QueryResponse<T>> Query<T>(Query queryOptions) where T : IGeometry
		{
			return Get<QueryResponse<T>, Query>(queryOptions);
		}
	}
}

