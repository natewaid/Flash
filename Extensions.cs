namespace Flash
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Reflection;


    public static class Extensions
    {
        static readonly BindingFlags _flags = BindingFlags.GetProperty
                                              | BindingFlags.IgnoreCase
                                              | BindingFlags.Instance
                                              | BindingFlags.Public;


        /// <summary>
        ///
        /// </summary>
        /// <param name="collection"></param>
        /// <returns></returns>
        public static DataTable AsItemDataTable(this IEnumerable collection)
        {
            Type first = null;
            var table = new DataTable();

            foreach (var item in collection)
            {
                if (first == null)
                {
                    first = item.GetType();

                    if (!Utils.IsSimpleType(first))
                    {
                        break;
                    }

                    var column = new DataColumn
                    {
                        ColumnName = "item",
                        DataType = first
                    };

                    if (first == typeof(string))
                        column.MaxLength = 2000;

                    table.Columns.Add(column);
                }

                if (item.GetType() != first)
                {
                    continue;
                }

                var row = table.NewRow();
                row["item"] = item;
                table.Rows.Add(row);
            }

            return table;
        }


        /// <summary>
        ///
        /// </summary>
        /// <param name="collection"></param>
        /// <returns></returns>
        public static DataTable AsDataTable(this IEnumerable collection)
        {
            var table = new DataTable();
            var type = collection.GetType().GetGenericArguments()[0];

            if (type == null)
            {
                return table;
            }

            var props = type
                .GetProperties(_flags)
                .Where(p => p.CanRead)
                .Where(p => p.GetCustomAttribute<IgnoreSetAttribute>() == null)
                .OrderBy(p => p.Name)
                .ToArray();

            foreach (var prop in props)
            {
                var t = prop.PropertyType;

                var ga = t.GenericTypeArguments.FirstOrDefault();
                if (ga != null)
                {
                    t = ga;
                }

                table.Columns.Add(prop.Name, t);
            }

            foreach (var item in collection)
            {
                var row = table.NewRow();

                foreach (var prop in props)
                {
                    var v = prop.GetValue(item);
                    row[prop.Name] = v ?? DBNull.Value;
                }

                table.Rows.Add(row);
            }

            return table;

        }

        /// <summary>
        /// Converts an IEnumerable to a DataTable using the properties as columns and the items as rows
        /// <remarks>It does not handle Nullable types well. DateTime? is handled others may not be as of yet.</remarks>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <returns></returns>
        public static DataTable AsDataTable<T>(this IEnumerable<T> collection)
        {
            var table = new DataTable();

            var props = typeof(T)
                .GetProperties(_flags)
                .Where(p => p.CanRead)
                .Where(p => p.GetCustomAttribute<IgnoreSetAttribute>() == null)
                .OrderBy(p => p.Name)
                .ToArray();

            foreach (var prop in props)
            {
                var t = prop.PropertyType;

                var ga = t.GenericTypeArguments.FirstOrDefault();
                if (ga != null)
                {
                    t = ga;
                }

                table.Columns.Add(prop.Name, t);
            }

            collection.ToList().ForEach(item =>
            {
                var row = table.NewRow();

                foreach (var prop in props)
                {
                    var v = prop.GetValue(item);
                    row[prop.Name] = v ?? DBNull.Value;
                }

                table.Rows.Add(row);
            });

            return table;
        }
    }
}
