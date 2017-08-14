namespace Flash
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    internal static class Utils
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="name"></param>
        /// <param name="remainingName"></param>
        /// <param name="value"></param>
        /// <param name="type"></param>
        /// <param name="flags"></param>
        public static void BindProperty(object obj, string name, string remainingName, object value, Type type, BindingFlags flags)
        {
            var prop = type.GetProperty(name, flags);
            if (prop == null) return;

            var pv = prop.GetValue(obj);
            if (pv == null)
            {
                var constructor = prop.PropertyType.GetConstructor(new Type[] { });
                if (constructor == null) return;
                pv = constructor.Invoke(new object[] { });
            }

            BindProperty(pv, remainingName, value, pv.GetType(), flags);
            prop.SetValue(obj, pv);
        }

        internal static string ToPascalCase(string s)
        {
            var result = new StringBuilder();
            var lastChar = new char();
            foreach (var c in s)
            {
                if (char.IsUpper(c) && char.IsLower(lastChar))
                {
                    result.Append("_");
                }
                result.Append(c.ToString().ToLower());
                lastChar = c;
            }

            return result.ToString();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="type"></param>
        /// <param name="flags"></param>
        public static void BindProperty(object obj, string name, object value, Type type, BindingFlags flags)
        {
            if (name.Contains("."))
            {
                var names = name.Split('.');
                BindProperty(obj, names[0], string.Join(".", names.Skip(1)), value, type, flags);
                return;
            }

            var prop = type.GetProperty(name, flags);

            if (prop == null)
            {
                prop = type.GetProperty(ToPascalCase(name), flags);
            }

            if (prop == null)
            {
                prop = type.GetProperty(name.Replace("_", ""), flags);
            }

            if (prop == null || prop.GetCustomAttribute<IgnoreAttribute>() != null) return;

            var ft = prop.PropertyType;

            if (ft.IsEnum)
            {
                value = Enum.Parse(ft, (string)value, true);
            }

            prop.SetValue(obj, value);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static IEnumerable<KeyValuePair<string, object>> ObjectToDictionary(object obj)
        {
            if (obj == null) return null;

            return obj.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetField)
                .ToDictionary(k => "@" + k.Name, v => v.GetValue(obj));
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="rdr"></param>
        /// <param name="fn"></param>
        public static void ProcessRows(IDataReader rdr, Action<IDataRecord> fn)
        {
            while (rdr.Read())
            {
                try
                {
                    fn(rdr);
                }
                catch (Exception e)
                {
                    throw;
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="outParams"></param>
        /// <param name="outputParameters"></param>
        public static void SetOutputParameters(IDbCommand cmd, IEnumerable<KeyValuePair<string, object>> outParams, object outputParameters)
        {
            foreach (var pair in outParams)
            {
                var t = outputParameters.GetType();
                var param = (IDataParameter)cmd.Parameters[pair.Key];
                if (param == null || param.Value == null) continue;

                var prop = t.GetProperty(param.ParameterName.TrimStart('@'),
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.SetProperty |
                    BindingFlags.IgnoreCase);

                prop?.SetValue(outputParameters, param.Value);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="parameters"></param>
        public static void SetParameters(IDbCommand cmd, object parameters)
        {
            if (parameters == null) return;

            var dictObject = parameters as IDictionary<string, object>;
            if (dictObject != null)
            {
                SetParameters(cmd, dictObject);
                return;
            }

            var enumarableObject = parameters as IEnumerable<KeyValuePair<string, object>>;
            if (enumarableObject != null)
            {
                SetParameters(cmd, enumarableObject);
                return;
            }

            SetParameters(cmd, ObjectToDictionary(parameters));
        }


        public static bool IsSimpleType(Type t)
        {
            return t.IsPrimitive
                   || t == typeof(string)
                   || t == typeof(DateTime)
                   || t == typeof(decimal);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="parameters"></param>
        /// <param name="f"></param>
        public static void SetParameters(IDbCommand cmd, IEnumerable<KeyValuePair<string, object>> parameters, Func<IDbDataParameter, IDbDataParameter> f = null)
        {
            if (parameters == null) return;

            foreach (var param in parameters)
            {
                var dbParam = (IDbDataParameter)new SqlParameter(param.Key, param.Value);

                if (f != null)
                {
                    dbParam = f(dbParam);
                }

                if (param.Value is DataTable)
                {
                    var p = (SqlParameter)dbParam;
                    p.SqlDbType = SqlDbType.Structured;
                    dbParam = p;
                }

                else if (param.Value is IEnumerable
                    && param.Value.GetType() != typeof(string)
                    && param.Value.GetType() != typeof(byte[]))
                {

                    var data = (IEnumerable)param.Value;

                    var p = (SqlParameter)dbParam;
                    p.SqlDbType = SqlDbType.Structured;

                    Type baseType = null;
                    var enumerator = data.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        baseType = enumerator.Current.GetType();
                        dbParam.Value = baseType == null || Utils.IsSimpleType(baseType) ? data.AsItemDataTable() : data.AsDataTable();
                    }
                    else
                    {
                        continue;
                    }
                }

                cmd.Parameters.Add(dbParam);
            }
        }
    }
}
