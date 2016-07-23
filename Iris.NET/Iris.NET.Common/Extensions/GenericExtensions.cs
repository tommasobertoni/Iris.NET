using System;
using System.Collections.Generic;
using System.Text;

namespace Iris.NET
{
    public static class GenericExtensions
    {
        /// <summary>
        /// Builds a string containing the messages and stacktraces of all the inner exceptions.
        /// </summary>
        /// <param name="ex">The root exception.</param>
        /// <returns>The full exception message.</returns>
        public static string GetFullExceptionMessage(this Exception ex)
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

        /// <summary>
        /// Helper method to iterate through an IEnumerable and invoke an action for every item in it.
        /// </summary>
        /// <typeparam name="T">The generic type of the IEnumerable.</typeparam>
        /// <param name="enumerable">The enumerable to iterate.</param>
        /// <param name="action">The action to invoke for each item.</param>
        public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> action)
        {
            if (enumerable != null)
                foreach (T item in enumerable)
                    action?.Invoke(item);
        }
    }
}
