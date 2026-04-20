using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlatformA.Library.Helper
{
    public static class Base62Converter
    {
        // 0-9, a-z, A-Z (총 62개 문자)
        private const string Base62Chars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

        // 숫자(Long) -> 문자열(Base62)
        public static string Encode(long id)
        {
            //if (id == 0) return "0";

            //var sb = new StringBuilder();
            //while (id > 0)
            //{
            //    int remainder = (int)(id % 62);
            //    sb.Insert(0, Base62Chars[remainder]);
            //    id /= 62;
            //}
            //return sb.ToString();

            // 간단하고 성능 좋은 개선법
            var sb = new StringBuilder();
            while (id > 0)
            {
                int remainder = (int)(id % 62);
                sb.Append(Base62Chars[remainder]); // O(1) 뒤로 붙이기
                id /= 62;
            }
            // 결과물을 뒤집어서 반환
            char[] chars = sb.ToString().ToCharArray();
            Array.Reverse(chars);
            return new string(chars);
        }

        // 문자열(Base62) -> 숫자(Long) 
        // (나중에 DB 조회 없이 ID를 역추적할 때 쓰임)
        public static long Decode(string base62)
        {
            long id = 0;
            foreach (char c in base62)
            {
                id = id * 62 + Base62Chars.IndexOf(c);
            }
            return id;
        }
    }
}
