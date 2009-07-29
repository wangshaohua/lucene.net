/**
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using NUnit.Framework;

using WhitespaceAnalyzer = Lucene.Net.Analysis.WhitespaceAnalyzer;
using Document = Lucene.Net.Documents.Document;
using Field = Lucene.Net.Documents.Field;
using IndexWriter = Lucene.Net.Index.IndexWriter;
using Term = Lucene.Net.Index.Term;
using MaxFieldLength = Lucene.Net.Index.IndexWriter.MaxFieldLength;
using IndexSearcher = Lucene.Net.Search.IndexSearcher;
using PhraseQuery = Lucene.Net.Search.PhraseQuery;
using RAMDirectory = Lucene.Net.Store.RAMDirectory;

namespace Lucene.Net.Search
{
    [TestFixture]
    public class TestSloppyPhraseQuery
    {

        private static readonly string S_1 = "A A A";
        private static readonly string S_2 = "A 1 2 3 A 4 5 6 A";

        private static readonly Document DOC_1 = makeDocument("X " + S_1 + " Y");
        private static readonly Document DOC_2 = makeDocument("X " + S_2 + " Y");
        private static readonly Document DOC_3 = makeDocument("X " + S_1 + " A Y");
        private static readonly Document DOC_1_B = makeDocument("X " + S_1 + " Y N N N N " + S_1 + " Z");
        private static readonly Document DOC_2_B = makeDocument("X " + S_2 + " Y N N N N " + S_2 + " Z");
        private static readonly Document DOC_3_B = makeDocument("X " + S_1 + " A Y N N N N " + S_1 + " A Y");
        private static readonly Document DOC_4 = makeDocument("A A X A X B A X B B A A X B A A");

        private static readonly PhraseQuery QUERY_1 = makePhraseQuery(S_1);
        private static readonly PhraseQuery QUERY_2 = makePhraseQuery(S_2);
        private static readonly PhraseQuery QUERY_4 = makePhraseQuery("X A A");

        /**
         * Test DOC_4 and QUERY_4.
         * QUERY_4 has a fuzzy (len=1) match to DOC_4, so all slop values > 0 should succeed.
         * But only the 3rd sequence of A's in DOC_4 will do.
         */
        [Test]
        public void TestDoc4_Query4_All_Slops_Should_match()
        {
            for (int slop = 0; slop < 30; slop++)
            {
                int numResultsExpected = slop < 1 ? 0 : 1;
                checkPhraseQuery(DOC_4, QUERY_4, slop, numResultsExpected);
            }
        }

        /**
         * Test DOC_1 and QUERY_1.
         * QUERY_1 has an exact match to DOC_1, so all slop values should succeed.
         * Before LUCENE-1310, a slop value of 1 did not succeed.
         */
        [Test]
        public void TestDoc1_Query1_All_Slops_Should_match()
        {
            for (int slop = 0; slop < 30; slop++)
            {
                float score1 = checkPhraseQuery(DOC_1, QUERY_1, slop, 1);
                float score2 = checkPhraseQuery(DOC_1_B, QUERY_1, slop, 1);
                Assert.IsTrue(score2 > score1, "slop=" + slop + " score2=" + score2 + " should be greater than score1 " + score1);
            }
        }

        /**
         * Test DOC_2 and QUERY_1.
         * 6 should be the minimum slop to make QUERY_1 match DOC_2.
         * Before LUCENE-1310, 7 was the minimum.
         */
        [Test]
        public void TestDoc2_Query1_Slop_6_or_more_Should_match()
        {
            for (int slop = 0; slop < 30; slop++)
            {
                int numResultsExpected = slop < 6 ? 0 : 1;
                float score1 = checkPhraseQuery(DOC_2, QUERY_1, slop, numResultsExpected);
                if (numResultsExpected > 0)
                {
                    float score2 = checkPhraseQuery(DOC_2_B, QUERY_1, slop, 1);
                    Assert.IsTrue(score2 > score1, "slop=" + slop + " score2=" + score2 + " should be greater than score1 " + score1);
                }
            }
        }

        /**
         * Test DOC_2 and QUERY_2.
         * QUERY_2 has an exact match to DOC_2, so all slop values should succeed.
         * Before LUCENE-1310, 0 succeeds, 1 through 7 fail, and 8 or greater succeeds.
         */
        [Test]
        public void TestDoc2_Query2_All_Slops_Should_match()
        {
            for (int slop = 0; slop < 30; slop++)
            {
                float score1 = checkPhraseQuery(DOC_2, QUERY_2, slop, 1);
                float score2 = checkPhraseQuery(DOC_2_B, QUERY_2, slop, 1);
                Assert.IsTrue(score2 > score1, "slop=" + slop + " score2=" + score2 + " should be greater than score1 " + score1);
            }
        }

        /**
         * Test DOC_3 and QUERY_1.
         * QUERY_1 has an exact match to DOC_3, so all slop values should succeed.
         */
        [Test]
        public void TestDoc3_Query1_All_Slops_Should_match()
        {
            for (int slop = 0; slop < 30; slop++)
            {
                float score1 = checkPhraseQuery(DOC_3, QUERY_1, slop, 1);
                float score2 = checkPhraseQuery(DOC_3_B, QUERY_1, slop, 1);
                Assert.IsTrue(score2 > score1, "slop=" + slop + " score2=" + score2 + " should be greater than score1 " + score1);
            }
        }

        private float checkPhraseQuery(Document doc, PhraseQuery query, int slop, int expectedNumResults)
        {
            query.SetSlop(slop);

            RAMDirectory ramDir = new RAMDirectory();
            WhitespaceAnalyzer analyzer = new WhitespaceAnalyzer();
            IndexWriter writer = new IndexWriter(ramDir, analyzer, MaxFieldLength.UNLIMITED);
            writer.AddDocument(doc);
            writer.Close();

            IndexSearcher searcher = new IndexSearcher(ramDir);
            TopDocs td = searcher.Search(query, null, 10);
            //System.out.println("slop: "+slop+"  query: "+query+"  doc: "+doc+"  Expecting number of hits: "+expectedNumResults+" maxScore="+td.getMaxScore());
            Assert.AreEqual(expectedNumResults, td.totalHits, "slop: " + slop + "  query: " + query + "  doc: " + doc + "  Wrong number of hits");

            //QueryUtils.check(query,searcher);

            searcher.Close();
            ramDir.Close();

            return td.GetMaxScore();
        }

        private static Document makeDocument(string docText)
        {
            Document doc = new Document();
            Field f = new Field("f", docText, Field.Store.NO, Field.Index.ANALYZED);
            f.SetOmitNorms(true);
            doc.Add(f);
            return doc;
        }

        private static PhraseQuery makePhraseQuery(string terms)
        {
            PhraseQuery query = new PhraseQuery();
            string[] t = terms.Split(new string[] { " " }, System.StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < t.Length; i++)
            {
                query.Add(new Term("f", t[i]));
            }
            return query;
        }

    }
}
