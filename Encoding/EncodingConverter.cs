using System;
using System.Text;
using System.Text.Encodings;

namespace HtmlToGmi
{
	public class EncodingConverter
	{
        public static string ConvertStringEncoding(string txt, Encoding srcEncoding, Encoding dstEncoding)
        {
            

            if (string.IsNullOrEmpty(txt))
            {
                return txt;
            }

            if (srcEncoding == null)
            {
                throw new System.ArgumentNullException(nameof(srcEncoding));
            }

            if (dstEncoding == null)
            {
                throw new System.ArgumentNullException(nameof(dstEncoding));
            }

            var srcBytes = srcEncoding.GetBytes(txt);
            var dstBytes = Encoding.Convert(srcEncoding, dstEncoding, srcBytes);
            return dstEncoding.GetString(dstBytes);
        }

        public static string Convert1252(string text)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return ConvertStringEncoding(text, Encoding.UTF8, Encoding.GetEncoding(1252));
        }


        public static string Convert1252(byte[] bytes)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding("windows-1252").GetString(bytes);
        }

    }
}

