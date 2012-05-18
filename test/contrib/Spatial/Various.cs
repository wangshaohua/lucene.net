﻿using System;
using Lucene.Net.Analysis;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Spatial;
using Lucene.Net.Spatial.Prefix;
using Lucene.Net.Spatial.Prefix.Tree;
using Lucene.Net.Store;
using NUnit.Framework;
using Spatial4n.Core.Context;
using Spatial4n.Core.Distance;
using Spatial4n.Core.Query;
using Spatial4n.Core.Shapes;

namespace Lucene.Net.Contrib.Spatial.Test
{
	[TestFixture]
	public class Various
	{
		private Directory _directory;
		private IndexSearcher _searcher;
		private IndexWriter _writer;
		protected SpatialStrategy<SimpleSpatialFieldInfo> strategy;
		protected SimpleSpatialFieldInfo fieldInfo;
		protected readonly SpatialContext ctx = SpatialContext.GEO_KM;
		protected readonly bool storeShape = true;
		private int maxLength;

		[SetUp]
		protected void SetUp()
		{
			maxLength = GeohashPrefixTree.GetMaxLevelsPossible();
			fieldInfo = new SimpleSpatialFieldInfo(GetType().Name);
			strategy = new RecursivePrefixTreeStrategy(new GeohashPrefixTree(ctx, maxLength));

			_directory = new RAMDirectory();
			_writer = new IndexWriter(_directory, new WhitespaceAnalyzer(), true, IndexWriter.MaxFieldLength.UNLIMITED);
		}

		private void AddPoint(IndexWriter writer, String name, double lat, double lng)
		{
			var doc = new Document();
			doc.Add(new Field("name", name, Field.Store.YES, Field.Index.ANALYZED));
			Shape shape = ctx.MakePoint(lat, lng);
			foreach (var f in strategy.CreateFields(fieldInfo, shape, true, storeShape))
			{
				if (f != null)
				{ // null if incompatibleGeometry && ignore
					doc.Add(f);
				}
			}
			writer.AddDocument(doc);
		}

		[Test]
		public void RadiusOf15Something()
		{
			// Origin
			const double _lat = 45.829507799999988;
			const double _lng = -73.800524699999983;

			//Locations
			AddPoint(_writer, "The location doc we are after", _lat, _lng);

			_writer.Commit();
			_writer.Close();

			_searcher = new IndexSearcher(_directory, true);

			ExecuteSearch(45.831909, -73.810322, ctx.GetUnits().Convert(150000, DistanceUnits.MILES), 1);
			ExecuteSearch(45.831909, -73.810322, ctx.GetUnits().Convert(15000, DistanceUnits.MILES), 1);
			ExecuteSearch(45.831909, -73.810322, ctx.GetUnits().Convert(1500, DistanceUnits.MILES), 1);

			_searcher.Close();
			_directory.Close();
		}

		private void ExecuteSearch(double lat, double lng, double radius, int expectedResults)
		{
			var dq = strategy.MakeQuery(new SpatialArgs(SpatialOperation.IsWithin, ctx.MakeCircle(lat, lng, radius)), fieldInfo);
			Console.WriteLine(dq);

			//var dsort = new DistanceFieldComparatorSource(dq.DistanceFilter);
			//Sort sort = new Sort(new SortField("foo", dsort, false));

			// Perform the search, using the term query, the distance filter, and the
			// distance sort
			TopDocs hits = _searcher.Search(dq, 10);
			int results = hits.TotalHits;
			ScoreDoc[] scoreDocs = hits.ScoreDocs;

			// Get a list of distances
			//Dictionary<int, Double> distances = dq.DistanceFilter.Distances;

			//Console.WriteLine("Distance Filter filtered: " + distances.Count);
			//Console.WriteLine("Results: " + results);

			//Assert.AreEqual(expectedResults, distances.Count); // fixed a store of only needed distances
			Assert.AreEqual(expectedResults, results);
		}

		[Test]
		public void LUCENENET462()
		{
			Console.WriteLine("LUCENENET462");

			// Origin
			const double _lat = 51.508129;
			const double _lng = -0.128005;

			// Locations
			AddPoint(_writer, "Location 1", 51.5073802128877, -0.124669075012207);
			AddPoint(_writer, "Location 2", 51.5091, -0.1235);
			AddPoint(_writer, "Location 3", 51.5093, -0.1232);
			AddPoint(_writer, "Location 4", 51.5112531582845, -0.12509822845459);
			AddPoint(_writer, "Location 5", 51.5107, -0.123);
			AddPoint(_writer, "Location 6", 51.512, -0.1246);
			AddPoint(_writer, "Location 8", 51.5088760101322, -0.143165588378906);
			AddPoint(_writer, "Location 9", 51.5087958793819, -0.143508911132813);

			_writer.Commit();
			_writer.Close();

			_searcher = new IndexSearcher(_directory, true);

			// create a distance query
			var radius = ctx.GetUnits().Convert(1.0, DistanceUnits.MILES);
			var dq = strategy.MakeQuery(new SpatialArgs(SpatialOperation.IsWithin, ctx.MakeCircle(_lat, _lng, radius)), fieldInfo);
			Console.WriteLine(dq);

			//var dsort = new DistanceFieldComparatorSource(dq.DistanceFilter);
			//Sort sort = new Sort(new SortField("foo", dsort, false));

			// Perform the search, using the term query, the distance filter, and the
			// distance sort
			TopDocs hits = _searcher.Search(dq, 1000);
			int results = hits.TotalHits;
			foreach (var scoreDoc in hits.ScoreDocs)
			{
				Console.WriteLine(_searcher.Doc(scoreDoc.doc).Get("name"));
			}

			Assert.AreEqual(8, results);

			_searcher.Close();
			_directory.Close();
		}
	}
}
