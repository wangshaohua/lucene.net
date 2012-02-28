﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Tokenattributes;
using Lucene.Net.Index;
using Lucene.Net.Util;
using NUnit.Framework;

namespace Lucene.Net.Analyzers.Payloads
{
    [TestFixture]
    public class DelimitedPayloadTokenFilterTest : LuceneTestCase
    {
        [Test]
        public void TestPayloads()
        {
            var encoding = Encoding.UTF8;
            String test = "The quick|JJ red|JJ fox|NN jumped|VB over the lazy|JJ brown|JJ dogs|NN";
            DelimitedPayloadTokenFilter filter = new DelimitedPayloadTokenFilter(new WhitespaceTokenizer(new StringReader(test)));
            TermAttribute termAtt = filter.GetAttribute<TermAttribute>();
            PayloadAttribute payAtt = filter.GetAttribute<PayloadAttribute>();
            AssertTermEquals("The", filter, termAtt, payAtt, null);
            AssertTermEquals("quick", filter, termAtt, payAtt, encoding.GetBytes("JJ"));
            AssertTermEquals("red", filter, termAtt, payAtt, encoding.GetBytes("JJ"));
            AssertTermEquals("fox", filter, termAtt, payAtt, encoding.GetBytes("NN"));
            AssertTermEquals("jumped", filter, termAtt, payAtt, encoding.GetBytes("VB"));
            AssertTermEquals("over", filter, termAtt, payAtt, null);
            AssertTermEquals("the", filter, termAtt, payAtt, null);
            AssertTermEquals("lazy", filter, termAtt, payAtt, encoding.GetBytes("JJ"));
            AssertTermEquals("brown", filter, termAtt, payAtt, encoding.GetBytes("JJ"));
            AssertTermEquals("dogs", filter, termAtt, payAtt, encoding.GetBytes("NN"));
            Assert.False(filter.IncrementToken());
        }

        [Test]
        public void TestNext()
        {
            var encoding = Encoding.UTF8;
            String test = "The quick|JJ red|JJ fox|NN jumped|VB over the lazy|JJ brown|JJ dogs|NN";
            DelimitedPayloadTokenFilter filter = new DelimitedPayloadTokenFilter(new WhitespaceTokenizer(new StringReader(test)));
            AssertTermEquals("The", filter, null);
            AssertTermEquals("quick", filter, encoding.GetBytes("JJ"));
            AssertTermEquals("red", filter, encoding.GetBytes("JJ"));
            AssertTermEquals("fox", filter, encoding.GetBytes("NN"));
            AssertTermEquals("jumped", filter, encoding.GetBytes("VB"));
            AssertTermEquals("over", filter, null);
            AssertTermEquals("the", filter, null);
            AssertTermEquals("lazy", filter, encoding.GetBytes("JJ"));
            AssertTermEquals("brown", filter, encoding.GetBytes("JJ"));
            AssertTermEquals("dogs", filter, encoding.GetBytes("NN"));
            Assert.False(filter.IncrementToken());
        }


        [Test]
        public void TestFloatEncoding()
        {
            String test = "The quick|1.0 red|2.0 fox|3.5 jumped|0.5 over the lazy|5 brown|99.3 dogs|83.7";
            DelimitedPayloadTokenFilter filter = new DelimitedPayloadTokenFilter(new WhitespaceTokenizer(new StringReader(test)), '|', new FloatEncoder());
            TermAttribute termAtt = filter.GetAttribute<TermAttribute>();
            PayloadAttribute payAtt = filter.GetAttribute<PayloadAttribute>();
            AssertTermEquals("The", filter, termAtt, payAtt, null);
            AssertTermEquals("quick", filter, termAtt, payAtt, PayloadHelper.EncodeFloat(1.0f));
            AssertTermEquals("red", filter, termAtt, payAtt, PayloadHelper.EncodeFloat(2.0f));
            AssertTermEquals("fox", filter, termAtt, payAtt, PayloadHelper.EncodeFloat(3.5f));
            AssertTermEquals("jumped", filter, termAtt, payAtt, PayloadHelper.EncodeFloat(0.5f));
            AssertTermEquals("over", filter, termAtt, payAtt, null);
            AssertTermEquals("the", filter, termAtt, payAtt, null);
            AssertTermEquals("lazy", filter, termAtt, payAtt, PayloadHelper.EncodeFloat(5.0f));
            AssertTermEquals("brown", filter, termAtt, payAtt, PayloadHelper.EncodeFloat(99.3f));
            AssertTermEquals("dogs", filter, termAtt, payAtt, PayloadHelper.EncodeFloat(83.7f));
            Assert.False(filter.IncrementToken());
        }

        [Test]
        public void TestIntEncoding()
        {
            String test = "The quick|1 red|2 fox|3 jumped over the lazy|5 brown|99 dogs|83";
            DelimitedPayloadTokenFilter filter = new DelimitedPayloadTokenFilter(new WhitespaceTokenizer(new StringReader(test)), '|', new IntegerEncoder());
            TermAttribute termAtt = filter.GetAttribute<TermAttribute>();
            PayloadAttribute payAtt = filter.GetAttribute<PayloadAttribute>();
            AssertTermEquals("The", filter, termAtt, payAtt, null);
            AssertTermEquals("quick", filter, termAtt, payAtt, PayloadHelper.EncodeInt(1));
            AssertTermEquals("red", filter, termAtt, payAtt, PayloadHelper.EncodeInt(2));
            AssertTermEquals("fox", filter, termAtt, payAtt, PayloadHelper.EncodeInt(3));
            AssertTermEquals("jumped", filter, termAtt, payAtt, null);
            AssertTermEquals("over", filter, termAtt, payAtt, null);
            AssertTermEquals("the", filter, termAtt, payAtt, null);
            AssertTermEquals("lazy", filter, termAtt, payAtt, PayloadHelper.EncodeInt(5));
            AssertTermEquals("brown", filter, termAtt, payAtt, PayloadHelper.EncodeInt(99));
            AssertTermEquals("dogs", filter, termAtt, payAtt, PayloadHelper.EncodeInt(83));
            Assert.False(filter.IncrementToken());
        }

        void AssertTermEquals(String expected, TokenStream stream, byte[] expectPay)
        {
            TermAttribute termAtt = stream.GetAttribute<TermAttribute>();
            PayloadAttribute payloadAtt = stream.GetAttribute<PayloadAttribute>();
            Assert.True(stream.IncrementToken());
            Assert.AreEqual(expected, termAtt.Term());
            Payload payload = payloadAtt.GetPayload();
            if (payload != null)
            {
                Assert.True(payload.Length() == expectPay.Length, payload.Length() + " does not equal: " + expectPay.Length);
                for (int i = 0; i < expectPay.Length; i++)
                {
                    Assert.True(expectPay[i] == payload.ByteAt(i), expectPay[i] + " does not equal: " + payload.ByteAt(i));

                }
            }
            else
            {
                Assert.True(expectPay == null, "expectPay is not null and it should be");
            }
        }

        void AssertTermEquals(String expected, TokenStream stream, TermAttribute termAtt, PayloadAttribute payAtt, byte[] expectPay)
        {
            Assert.True(stream.IncrementToken());
            Assert.AreEqual(expected, termAtt.Term());
            Payload payload = payAtt.GetPayload();
            if (payload != null)
            {
                Assert.True(payload.Length() == expectPay.Length, payload.Length() + " does not equal: " + expectPay.Length);
                for (int i = 0; i < expectPay.Length; i++)
                {
                    Assert.True(expectPay[i] == payload.ByteAt(i), expectPay[i] + " does not equal: " + payload.ByteAt(i));

                }
            }
            else
            {
                Assert.True(expectPay == null, "expectPay is not null and it should be");
            }
        }
    }
}
