using System;
using System.Collections.Generic;
using System.Text;

namespace Iris.NET
{
    public static class GenericExtensions
    {
        public static string GetFullException(this Exception ex)
        {
            StringBuilder fullExceptionBuilder = new StringBuilder();
            var innerLevel = 0;

            do
            {
                var exceptionFormat = $"{nameof(ex.Message)}: {ex.Message};{nameof(ex.StackTrace)}: {ex.StackTrace}";
                if (innerLevel > 0)
                    exceptionFormat = $";(inner level: {++innerLevel}) {exceptionFormat}";

                fullExceptionBuilder.Append(exceptionFormat);
                ex = ex.InnerException;
            } while (ex != null);
            
            return fullExceptionBuilder.ToString();
        }

        public static void ForEach<T>(this IrisConcurrentHashSet<T> hashSet, Action<T> action)
        {
            if (hashSet != null)
                foreach (T item in hashSet)
                    action?.Invoke(item);
        }
    }
}
