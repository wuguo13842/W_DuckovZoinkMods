using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace ZoinkModdingLibrary.Extentions
{
    public static class JsonExtentios
    {
        public static List<T> ToList<T>(this JArray jArray)
        {
            List<T> result = new List<T>();
            foreach (var item in jArray)
            {
                T? value = item.ToObject<T>();
                if (value == null)
                    throw new NullReferenceException($"Failed to convert to {typeof(T)}");
                result.Add(value);
            }
            return result;
        }
    }
}
