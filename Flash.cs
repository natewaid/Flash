namespace Flash
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Data;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Reflection;

    public static class Flash
    {
        private static string ProcedureVersioning(string proc)
        {
            if (string.IsNullOrWhiteSpace(proc))
            {
                return proc;
            }

            var original = proc;
            var actual = proc;
            var config = ConfigurationManager.GetSection("dbProcVersioning") as IDictionary;

            while (config != null && config.Contains(actual))
            {
                actual = config[actual] as string;
            }

            return actual;
        }

        /// <summary>
        ///
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="rdr"></param>
        /// <returns></returns>
        public static T Bind<T>(IDataRecord rdr) where T : new()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.SetProperty | BindingFlags.IgnoreCase | BindingFlags.Public;

            var t = new T();
            var type = typeof(T);

            if (rdr.FieldCount == 1 && rdr.GetFieldType(0) == type)
            {
                return (T)rdr.GetValue(0);
            }

            for (var i = 0; i < rdr.FieldCount; i++)
            {
                if (rdr.IsDBNull(i))
                {
                    continue;
                }
                var n = rdr.GetName(i);
                if (n == null)
                {
                    continue;
                }
                var v = rdr.GetValue(i);
                Utils.BindProperty(t, n, v, type, flags);
            }

            return t;
        }

        /// <summary>
        /// Executes a sql command.
        /// </summary>
        /// <param name="con"></param>
        /// <param name="sql"></param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
        public static void Execute(IDbConnection con, string sql)
        {
            try
            {
                using (con)
                {
                    using (var cmd = con.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.CommandType = CommandType.Text;
                        con.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e)
            {
                throw;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="con"></param>
        /// <param name="proc"></param>
        /// <param name="parameters"></param>
        /// <param name="outputParameters"></param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
        public static void Execute(IDbConnection con, string proc, IEnumerable<KeyValuePair<string, object>> parameters, object outputParameters = null)
        {
            parameters = parameters ?? new Dictionary<string, object>();
            var outParams = (Utils.ObjectToDictionary(outputParameters) ?? new Dictionary<string, object>()).ToArray();

            proc = ProcedureVersioning(proc);

            try
            {
                using (con)
                {
                    using (var cmd = con.CreateCommand())
                    {
                        cmd.CommandText = proc;
                        cmd.CommandType = CommandType.StoredProcedure;

                        Utils.SetParameters(cmd, parameters);
                        Utils.SetParameters(cmd, outParams, p =>
                        {
                            p.Direction = ParameterDirection.Output;
                            return p;
                        });

                        con.Open();
                        cmd.ExecuteNonQuery();
                        Utils.SetOutputParameters(cmd, outParams, outputParameters);
                    }
                }
            }
            catch (Exception e)
            {
                throw;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="con"></param>
        /// <param name="proc"></param>
        /// <param name="parameters"></param>
        public static void Execute(IDbConnection con, string proc, object parameters)
        {
            Execute(con, proc, Utils.ObjectToDictionary(parameters));
        }

        /// <summary>
        /// gets an enumerable of type T by automatically binding a data record to an object. It does not handle complex objects.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="con"></param>
        /// <param name="sql"></param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
        public static IEnumerable<T> List<T>(IDbConnection con, string sql) where T : new()
        {
            var results = new List<T>();

            try
            {
                using (con)
                {
                    using (var cmd = con.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.CommandType = CommandType.Text;
                        con.Open();
                        Utils.ProcessRows(cmd.ExecuteReader(), r => results.Add(Bind<T>(r)));
                    }
                }
            }
            catch (Exception e)
            {
                throw;
            }

            return results;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="con"></param>
        /// <param name="proc"></param>
        /// <param name="parameters"></param>
        /// <param name="fns"></param>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Security", "CA2100")]
        public static void List(IDbConnection con, string proc, object parameters, params Action<IDataRecord>[] fns)
        {
            proc = ProcedureVersioning(proc);

            try
            {


                using (con)
                {
                    using (var cmd = con.CreateCommand())
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandText = proc;
                        Utils.SetParameters(cmd, parameters);
                        con.Open();

                        using (var r = cmd.ExecuteReader())
                        {
                            foreach (var fn in fns)
                            {
                                Utils.ProcessRows(r, fn);
                                r.NextResult();
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw;

            }
        }

        /// <summary>
        /// executes a query performing the action on each row.
        /// </summary>
        /// <param name="con"></param>
        /// <param name="sql"></param>
        /// <param name="fn"></param>
        [SuppressMessage("Microsoft.Security", "CA2100")]
        public static void List(IDbConnection con, string sql, Action<IDataRecord> fn)
        {
            try
            {
                using (con)
                {
                    using (var cmd = con.CreateCommand())
                    {
                        cmd.CommandType = CommandType.Text;
                        cmd.CommandText = sql;
                        con.Open();
                        Utils.ProcessRows(cmd.ExecuteReader(), fn);
                    }
                }
            }
            catch (Exception e)
            {
                throw;
            }
        }

        /// <summary>
        /// gets an enumerable of type T by automatically binding a data record to an object. It does not handle complex objects. The data call
        /// is made by using a stored procedure with the paramaters provided. parameter names in the key value pairs should start with the @ symbol.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="con"></param>
        /// <param name="proc"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
        public static IEnumerable<T> List<T>(IDbConnection con, string proc, IEnumerable<KeyValuePair<string, object>> parameters) where T : new()
        {
            var results = new List<T>();
            proc = ProcedureVersioning(proc);

            try
            {

                using (con)
                {
                    using (var cmd = con.CreateCommand())
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandText = proc;
                        Utils.SetParameters(cmd, parameters);
                        con.Open();
                        Utils.ProcessRows(cmd.ExecuteReader(), r => results.Add(Bind<T>(r)));
                    }
                }
            }
            catch (Exception e)
            {
                throw;
            }

            return results;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="con"></param>
        /// <param name="proc"></param>
        /// <param name="parameters"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static IEnumerable<T> List<T>(IDbConnection con, string proc, object parameters) where T : new()
        {
            return List<T>(con, proc, Utils.ObjectToDictionary(parameters));
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="con"></param>
        /// <param name="proc"></param>
        /// <param name="parameters"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T Scalar<T>(IDbConnection con, string proc, object parameters)
        {
            return Scalar<T>(con, proc, Utils.ObjectToDictionary(parameters));
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="con"></param>
        /// <param name="sql"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Security", "CA2100")]
        public static T Scalar<T>(IDbConnection con, string sql)
        {
            var result = default(T);
            try
            {
                using (con)
                {
                    using (var cmd = con.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.CommandType = CommandType.Text;

                        con.Open();
                        var obj = cmd.ExecuteScalar();
                        if (obj != null) result = (T)obj;
                    }
                }
            }
            catch (Exception e)
            {
                throw;
            }
            return result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="con"></param>
        /// <param name="proc"></param>
        /// <param name="parameters"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Security", "CA2100")]
        public static T Scalar<T>(IDbConnection con, string proc, IEnumerable<KeyValuePair<string, object>> parameters)
        {
            proc = ProcedureVersioning(proc);

            var result = default(T);

            try
            {
                using (con)
                {
                    using (var cmd = con.CreateCommand())
                    {
                        cmd.CommandText = proc;
                        cmd.CommandType = CommandType.StoredProcedure;
                        Utils.SetParameters(cmd, parameters);
                        con.Open();
                        var obj = cmd.ExecuteScalar();
                        if (obj != null) result = (T)obj;
                    }
                }
            }
            catch (Exception e)
            {
                throw;
            }

            return result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="con"></param>
        /// <param name="proc"></param>
        /// <param name="parameters"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T Single<T>(IDbConnection con, string proc, object parameters) where T : new()
        {
            return Single<T>(con, proc, Utils.ObjectToDictionary(parameters));
        }

        /// <summary>
        /// gets a single value of type T by getting the first from the list of type T.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="con"></param>
        /// <param name="proc"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static T Single<T>(IDbConnection con, string proc, IEnumerable<KeyValuePair<string, object>> parameters) where T : new()
        {
            return List<T>(con, proc, parameters).FirstOrDefault();
        }

        /// <summary>
        /// gets a single value of type T by getting the first from the list of type T.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="con"></param>
        /// <param name="sql"></param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
        public static T Single<T>(IDbConnection con, string sql) where T : new()
        {
            return List<T>(con, sql).FirstOrDefault();
        }
    }
}
