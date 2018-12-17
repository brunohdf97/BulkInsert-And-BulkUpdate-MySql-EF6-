public static void BulkInsert<T>(this IList<T> entities) where T : class
        {
            var bd = new Models.Contexto();
            if (entities != null)
            {
                if (entities.Count < 500)
                {
                    bd.Set<T>().AddRange(entities);
                    bd.SaveChanges();
                    return;
                }

                var type = typeof(T);
                var objadpter = (bd as IObjectContextAdapter).ObjectContext.CreateObjectSet<T>();
                var objadptervalue = objadpter.EntitySet.MetadataProperties.FirstOrDefault(a => a.Name == "Configuration").Value;
                string tabletoinsert_name = objadptervalue.GetType().GetProperty("TableName").GetValue(objadptervalue) + "";

                using (var dbContextTransaction = bd.Database.BeginTransaction())
                {
                    try
                    {
                        if (type != null)
                        {
                            var properties = type.GetProperties().OrderBy(a => a.CustomAttributes.Any(b => b.GetType().Name == "KeyAttribute")).ToArray();
                            if (properties.Length > 0)
                            {
                                string[] sqlinsert_properties = new string[properties.Length];
                                bool theyhavejustonekey = properties.Sum(a => a.GetCustomAttributes(false).Count(b => b.GetType().Name == "KeyAttribute")) == 1;
                                for (int i = 0; i < properties.Length; i++)
                                {
                                    var property = properties[i];
                                    var customattributes = property.GetCustomAttributes();
                                    bool isprimarykey = property.GetCustomAttributes(false).Any(a => a.GetType().Name == "KeyAttribute");
                                    bool isvalidproperty = !customattributes.Any(a => a.GetType().Name == "NotMappedAttribute");

                                    if (isvalidproperty)
                                    {
                                        if (isprimarykey)
                                        {
                                            if (!theyhavejustonekey)
                                            {
                                                sqlinsert_properties[i] = property.Name;
                                            }
                                        }
                                        else
                                        {
                                            sqlinsert_properties[i] = property.Name;
                                        }
                                    }
                                }

                                List<string> sqlinsert_values = new List<string>();
                                Type[] nulltypes = new Type[] { typeof(int?), typeof(DateTime?), typeof(bool?), typeof(decimal?), typeof(double?), typeof(TimeSpan?) };
                                foreach (var entity in entities)
                                {
                                    var entity_type = entity.GetType();
                                    var entity_properties = entity_type.GetProperties().OrderBy(a => a.CustomAttributes.Any(b => b.GetType().Name == "KeyAttribute")).ToArray();
                                    string[] insertintovalue = new string[entity_properties.Length];
                                    for (int i = 0; i < entity_properties.Length; i++)
                                    {
                                        var entity_property = entity_properties[i];
                                        var customattributes = entity_property.GetCustomAttributes();
                                        bool isprimarykey = customattributes.Any(a => a.GetType().Name == "KeyAttribute");
                                        bool isvalidproperty = !customattributes.Any(a => a.GetType().Name == "NotMappedAttribute");

                                        if (isvalidproperty)
                                        {
                                            if (!isprimarykey || !theyhavejustonekey)
                                            {
                                                var value = entity_property.GetValue(entity);
                                                if (entity_property.PropertyType == typeof(int) || entity_property.PropertyType == typeof(int?))
                                                {
                                                    insertintovalue[i] = value == null ? "NULL" : value + "";
                                                }
                                                else if (entity_property.PropertyType == typeof(bool) || entity_property.PropertyType == typeof(bool?))
                                                {
                                                    insertintovalue[i] = value == null ? "NULL" : value == "False" ? "0" : "1";
                                                }
                                                else if (entity_property.PropertyType == typeof(double) || entity_property.PropertyType == typeof(double?))
                                                {
                                                    insertintovalue[i] = value == null ? "NULL" : value + "".Replace(",", ".");
                                                }
                                                else if (entity_property.PropertyType == typeof(decimal) || entity_property.PropertyType == typeof(decimal?))
                                                {
                                                    insertintovalue[i] = value == null ? "NULL" : value + "".Replace(",", ".");
                                                }
                                                else if (entity_property.PropertyType == typeof(DateTime) || entity_property.PropertyType == typeof(DateTime?))
                                                {
                                                    if (value == null)
                                                    {
                                                        insertintovalue[i] = "NULL";
                                                    }
                                                    else
                                                    {
                                                        insertintovalue[i] = "'" + string.Format("{0:yyyy-MM-dd hh:mm:ss}", (DateTime)value) + "'";

                                                    }
                                                }
                                                else if (nulltypes.Contains(entity_property.PropertyType))
                                                {
                                                    insertintovalue[i] = value == null ? "NULL" : "'" + value + "'";
                                                }
                                                else
                                                {
                                                    insertintovalue[i] = value == null ? "NULL" : "'" + value + "'";
                                                }
                                            }
                                        }
                                    }
                                    insertintovalue = insertintovalue.Where(a => a != null).ToArray();
                                    sqlinsert_values.Add("(" + string.Join(",", insertintovalue) + ")");
                                }

                                sqlinsert_properties = sqlinsert_properties.Where(a => a != null).ToArray();
                                string table_firstvalues = sqlinsert_values[0];
                                sqlinsert_values.RemoveAt(0);
                                string sqlinsert = string.Format("INSERT INTO {0} {1} VALUES {2}, {3};", "`" + tabletoinsert_name + "`", "(" + string.Join(",", sqlinsert_properties) + ")", table_firstvalues, string.Join(",", sqlinsert_values));
                                bd.Database.ExecuteSqlCommand(sqlinsert);
                                dbContextTransaction.Commit();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        dbContextTransaction.Rollback();
                        throw new Exception(e.Message, e);
                    }
                }
            }
        }

        public static void BulkUpdate<T>(this IList<T> entities) where T : class
        {
            var bd = new Models.Contexto();
            if (entities != null)
            {
                if (entities.Count < 500)
                {
                    foreach (var entity in entities)
                    {
                        bd.Entry(entity).State = EntityState.Modified;
                    }
                    bd.SaveChanges();
                    return;
                }

                string tabletemp_name = "tmp_t_" + DateTime.Now.Day + DateTime.Now.Hour +
                                                  DateTime.Now.Second + DateTime.Now.Millisecond;
                bool temptable_created = false;
                using (var dbContextTransaction = bd.Database.BeginTransaction())
                {
                    try
                    {
                        var type = typeof(T);
                        var objadpter = (bd as IObjectContextAdapter).ObjectContext.CreateObjectSet<T>();
                        var objadptervalue = objadpter.EntitySet.MetadataProperties.FirstOrDefault(a => a.Name == "Configuration").Value;
                        string tabletoupdate_name = objadptervalue.GetType().GetProperty("TableName").GetValue(objadptervalue) + "";
                        if (type != null)
                        {
                            var properties = type.GetProperties().OrderBy(a => a.CustomAttributes.Any(b => b.GetType().Name == "KeyAttribute")).ToArray();
                            if (properties.Length > 0)
                            {
                                string[] sqltemp_properties = new string[properties.Length];
                                string[] sqltemp_primarykeys = new string[properties.Length];
                                string[] sqltemp_insertintoproperties = new string[properties.Length];
                                string[] sqlupdate_setters = new string[properties.Length];
                                string[] sqlupdate_innerjoin_where = new string[properties.Length];

                                string sisunsigned;
                                string sisnullable;
                                for (int i = 0; i < properties.Length; i++)
                                {
                                    var property = properties[i];
                                    var customattributes = property.GetCustomAttributes();
                                    bool isprimarykey = property.GetCustomAttributes(false).Any(a => a.GetType().Name == "KeyAttribute");
                                    bool isvalidproperty = !customattributes.Any(a => a.GetType().Name == "NotMappedAttribute");

                                    if (isvalidproperty)
                                    {
                                        sqltemp_insertintoproperties[i] = property.Name;
                                        sqlupdate_setters[i] = "a." + property.Name + " = b." + property.Name + "";

                                        if (isprimarykey)
                                        {
                                            sqlupdate_innerjoin_where[i] = "a." + property.Name + " = b." + property.Name + "";
                                            sqltemp_primarykeys[i] += "`" + property.Name + "`";
                                        }

                                        if (property.PropertyType == typeof(int)
                                         || property.PropertyType == typeof(int?))
                                        {
                                            bool isnullable = property.PropertyType == typeof(int?);
                                            sisunsigned = !isnullable ? "unsigned" : "";
                                            sisnullable = isnullable ? "NULL" : "NOT NULL";
                                            sqltemp_properties[i] = "`" + property.Name + "` INT(10) " + sisunsigned + " " +
                                                                  sisnullable;
                                        }
                                        else if (property.PropertyType == typeof(string))
                                        {
                                            sisnullable = "NULL";
                                            sqltemp_properties[i] = "`" + property.Name + "` MEDIUMTEXT " + sisnullable;
                                        }
                                        else if (property.PropertyType == typeof(DateTime)
                                              || property.PropertyType == typeof(DateTime?))
                                        {
                                            sisnullable = property.PropertyType == typeof(DateTime?) ? "NULL" : "NOT NULL";
                                            sqltemp_properties[i] = "`" + property.Name + "` DATETIME " + sisnullable;
                                        }
                                        else if (property.PropertyType == typeof(char)
                                              || property.PropertyType == typeof(char?))
                                        {
                                            sisnullable = property.PropertyType == typeof(char?) ? "NULL" : "NOT NULL";
                                            sqltemp_properties[i] = "`" + property.Name + "` CHAR(30) " + sisnullable;
                                        }
                                        else if (property.PropertyType == typeof(bool)
                                              || property.PropertyType == typeof(bool?))
                                        {
                                            bool isnullable = property.PropertyType == typeof(bool?);
                                            sisunsigned = !isnullable ? "unsigned" : "";
                                            sisnullable = isnullable ? "NULL" : "NOT NULL";
                                            sqltemp_properties[i] = "`" + property.Name + "` TINYINT(1) " + sisunsigned + " " + sisnullable;
                                        }
                                        else if (property.PropertyType == typeof(decimal)
                                              || property.PropertyType == typeof(decimal?))
                                        {
                                            bool isnullable = property.PropertyType == typeof(decimal?);
                                            sisunsigned = !isnullable ? "unsigned" : "";
                                            sisnullable = isnullable ? "NULL" : "NOT NULL";
                                            sqltemp_properties[i] = "`" + property.Name + "` DECIMAL(10,2) " + sisunsigned + " " + sisnullable;
                                        }
                                        else if (property.PropertyType == typeof(double)
                                              || property.PropertyType == typeof(double?))
                                        {
                                            bool isnullable = property.PropertyType == typeof(double?);
                                            sisunsigned = !isnullable ? "unsigned" : "";
                                            sisnullable = isnullable ? "NULL" : "NOT NULL";
                                            sqltemp_properties[i] = "`" + property.Name + "` DOUBLE " + sisunsigned + " " + sisnullable;
                                        }
                                        else if (property.PropertyType == typeof(TimeSpan)
                                              || property.PropertyType == typeof(TimeSpan?))
                                        {
                                            sisnullable = property.PropertyType == typeof(TimeSpan?) ? "NULL" : "NOT NULL";
                                            sqltemp_properties[i] = "`" + property.Name + "` TIME " + sisnullable;
                                        }
                                    }
                                }
                                sqltemp_insertintoproperties = sqltemp_insertintoproperties.Where(a => a != null).ToArray();
                                sqlupdate_setters = sqlupdate_setters.Where(a => a != null).ToArray();
                                sqlupdate_innerjoin_where = sqlupdate_innerjoin_where.Where(a => a != null).ToArray();
                                sqltemp_primarykeys = sqltemp_primarykeys.Where(a => a != null).ToArray();
                                sqltemp_properties = sqltemp_properties.Where(a => a != null).ToArray();

                                //sqltemp_primarykeys = sqltemp_primarykeys.Where(a => a != null).ToArray();
                                string stemptable_properties = string.Join(",", sqltemp_properties);
                                string stemptable_keys = string.Join(",", sqltemp_primarykeys);

                                string sqltemptable = string.Format("CREATE TABLE {0}" +
                                                                    "( {1}, PRIMARY KEY {2} ) ENGINE=InnoDB DEFAULT CHARSET=latin1;", "`" + tabletemp_name + "`", stemptable_properties, "(" + stemptable_keys + ")");
                                bd.Database.ExecuteSqlCommand(sqltemptable);

                                List<string> sqltemp_insertintovalues = new List<string>();
                                Type[] nulltypes = new Type[] { typeof(int?), typeof(DateTime?), typeof(bool?), typeof(decimal?), typeof(double?), typeof(TimeSpan?) };
                                foreach (var entity in entities)
                                {
                                    var entity_type = entity.GetType();
                                    var entity_properties = entity_type.GetProperties().OrderBy(a => a.CustomAttributes.Any(b => b.GetType().Name == "KeyAttribute")).ToArray();
                                    string[] insertintovalue = new string[entity_properties.Length];
                                    for (int i = 0; i < entity_properties.Length; i++)
                                    {
                                        var entity_property = entity_properties[i];
                                        var value = entity_property.GetValue(entity);
                                        var customattributes = entity_property.GetCustomAttributes();
                                        //bool isprimarykey = entity_property.GetCustomAttributes(false).Any(a => a.GetType().Name == "KeyAttribute");
                                        bool isvalidproperty = !customattributes.Any(a => a.GetType().Name == "NotMappedAttribute");
                                        if (isvalidproperty)
                                        {
                                            if (entity_property.PropertyType == typeof(int) || entity_property.PropertyType == typeof(int?))
                                            {
                                                insertintovalue[i] = value == null ? "NULL" : value + "";
                                            }
                                            else if (entity_property.PropertyType == typeof(bool) || entity_property.PropertyType == typeof(bool?))
                                            {
                                                insertintovalue[i] = value == null ? "NULL" : value == "False" ? "0" : "1";
                                            }
                                            else if (entity_property.PropertyType == typeof(double) || entity_property.PropertyType == typeof(double?))
                                            {
                                                insertintovalue[i] = value == null ? "NULL" : value + "".Replace(",", ".");
                                            }
                                            else if (entity_property.PropertyType == typeof(decimal) || entity_property.PropertyType == typeof(decimal?))
                                            {
                                                insertintovalue[i] = value == null ? "NULL" : value + "".Replace(",", ".");
                                            }
                                            else if (entity_property.PropertyType == typeof(DateTime) || entity_property.PropertyType == typeof(DateTime?))
                                            {
                                                if (value == null)
                                                {
                                                    insertintovalue[i] = "NULL";
                                                }
                                                else
                                                {
                                                    insertintovalue[i] = "'" + string.Format("{0:yyyy-MM-dd hh:mm:ss}", (DateTime)value) + "'";

                                                }
                                            }
                                            else if (nulltypes.Contains(entity_property.PropertyType))
                                            {
                                                insertintovalue[i] = value == null ? "NULL" : "'" + value + "'";
                                            }
                                            else
                                            {
                                                insertintovalue[i] = value == null ? "NULL" : "'" + value + "'";
                                            }
                                        }

                                    }
                                    insertintovalue = insertintovalue.Where(a => a != null).ToArray();
                                    sqltemp_insertintovalues.Add("(" + string.Join(",", insertintovalue) + ")");
                                }

                                string table_firstvalues = sqltemp_insertintovalues[0];
                                sqltemp_insertintovalues.RemoveAt(0);
                                string sqlinsert = string.Format("INSERT INTO {0} ({1}) VALUES {2}, {3};", "`" + tabletemp_name + "`", string.Join(",", sqltemp_insertintoproperties), table_firstvalues, string.Join(", ", sqltemp_insertintovalues));
                                bd.Database.ExecuteSqlCommand(sqlinsert);
                                temptable_created = true;

                                string sqlupdate = string.Format("UPDATE {0} AS a " +
                                                                "INNER JOIN {1} AS b ON {2}" +
                                                                " SET {3};", "`" + tabletoupdate_name + "`", "`" + tabletemp_name + "`", string.Join(" AND ", sqlupdate_innerjoin_where), string.Join(",", sqlupdate_setters));

                                bd.Database.ExecuteSqlCommand(sqlupdate);
                                bd.Database.ExecuteSqlCommand("DROP TABLE " + tabletemp_name);
                                dbContextTransaction.Commit();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        if (temptable_created)
                            bd.ExecutaSql("DROP TABLE " + tabletemp_name);

                        dbContextTransaction.Rollback();
                        throw new Exception(e.Message, e);
                    }
                }
            }
        }
