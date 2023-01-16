namespace NethermindNode.Tests.Helpers
{
    public static class ExtensionMethods
    {
        public static string ToJoinedString<T>(this List<T> list)
        {
            return String.Join(',', list);
        }
    }
}
