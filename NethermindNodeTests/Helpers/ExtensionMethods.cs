using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NethermindNodeTests.Helpers
{
    public static class ExtensionMethods
    {
        public static string ToJoinedString<T>(this List<T> list)
        {
            return String.Join(',', list);
        }
    }
}
