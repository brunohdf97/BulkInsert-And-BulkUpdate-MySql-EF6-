public static string MySQLEscape(string str)
        {
            return Regex.Replace(str, @"[\@\?\'\x00'""\b\n\r\t\cZ\\%_]",
                delegate(Match match)
                {
                    string v = match.Value;
                    switch (v)
                    {
                        case "\x00":            // ASCII NUL (0x00) character
                            return "\\0";
                        case "\b":              // BACKSPACE character
                            return "\\b";
                        case "\n":              // NEWLINE (linefeed) character
                            return "\\n";
                        case "\r":              // CARRIAGE RETURN character
                            return "\\r";
                        case "\t":              // TAB
                            return "\\t";
                        case "\u001A":          // Ctrl-Z
                            return "\\Z";
                        default:
                            return "\\" + v;
                    }
                });
        } 

public static void BulkInsert<T>(this IList<T> entities) where T : class
        {
            var bd = new Models.Contexto();
            if (entities != null)
            {
                if (entities.Count < 100)
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
                            //var sqlinsert_uniquedt = "'" + string.Format("{0:yyyy-MM-dd hh:mm:ss}", bd.DataServidor()) + "'";
                            string sqlinsert_uniquedt = Guid.NewGuid() + "";
                            string sqlinsert_tempfieldname = "temp_bulk_unique_id";
                            List<string> sqlinsert_primarykeys = new List<string>();

                            DatabaseGeneratedOption[] validprimarykeys_options = new DatabaseGeneratedOption[]
                                                {
                                                    DatabaseGeneratedOption.Computed, 
                                                    DatabaseGeneratedOption.Identity, 
                                                };
                            var properties = type.GetProperties().OrderBy(a => a.CustomAttributes.Any(b => b.GetType().Name == "KeyAttribute")).ToArray();
                            if (properties.Length > 0)
                            {
                                string[] sqlinsert_properties = new string[properties.Length + 1];
                                //bool theyhavejustonekey = properties.Sum(a => a.GetCustomAttributes(false).Count(b => b.GetType().Name == "KeyAttribute")) == 1;
                                for (int i = 0; i < properties.Length; i++)
                                {
                                    var property = properties[i];
                                    var customattributes = property.GetCustomAttributes(false);

                                    bool isprimarykey = customattributes.Any(a => a.GetType().Name == "KeyAttribute");
                                    if (isprimarykey)
                                    {
                                        DatabaseGeneratedAttribute propdatabasegeneratednone = (DatabaseGeneratedAttribute)customattributes.FirstOrDefault(a => a.GetType().Name == "DatabaseGeneratedAttribute");
                                        if (propdatabasegeneratednone != null)
                                        {
                                            if (!validprimarykeys_options.Contains(propdatabasegeneratednone.DatabaseGeneratedOption))
                                            {
                                                isprimarykey = false;
                                            }
                                        }
                                    }

                                    bool isforeingkey = customattributes.Any(a => a.GetType().Name == "ForeignKeyAttribute");
                                    bool isvalidproperty = !customattributes.Any(a => a.GetType().Name == "NotMappedAttribute");
                                    bool isintornullint = property.PropertyType == typeof(int) ||
                                                             property.PropertyType == typeof(int?);
                                    bool isvalidint = true;
                                    if (isprimarykey)
                                    {
                                        sqlinsert_primarykeys.Add(property.Name);
                                        sqlinsert_primarykeys.Add("';'"); //separator for CONCAT MYSQL
                                        if (isintornullint)
                                        {
                                            var firstproperty = entities[0].GetType().GetProperty(property.Name);
                                            if (firstproperty != null)
                                            {
                                                var firstvalue = firstproperty.GetValue(entities[0]);
                                                isvalidint = ((firstvalue != null && (int)firstvalue != 0) || firstvalue == null);
                                            }

                                        }
                                    }

                                    if (!isforeingkey && isvalidproperty && (!isintornullint || isintornullint && isvalidint) && !isprimarykey)
                                    {
                                        sqlinsert_properties[i] = property.Name;
                                    }
                                }
                                sqlinsert_properties[properties.Length] = sqlinsert_tempfieldname;
                                sqlinsert_properties = sqlinsert_properties.Where(a => a != null).ToArray();

                                List<string> sqlinsert_values = new List<string>();
                                Type[] nulltypes = new Type[] { typeof(int?), typeof(DateTime?), typeof(bool?), typeof(decimal?), typeof(double?), typeof(TimeSpan?) };
                                foreach (var entity in entities)
                                {
                                    var entity_type = entity.GetType();
                                    var entity_properties = entity_type.GetProperties()
                                        .Where(a => sqlinsert_properties.Contains(a.Name))
                                        .OrderBy(a => a.CustomAttributes.Any(b => b.GetType().Name == "KeyAttribute"))
                                        .ToArray();
                                    string[] insertintovalue = new string[entity_properties.Length + 1];
                                    for (int i = 0; i < entity_properties.Length; i++)
                                    {
                                        var entity_property = entity_properties[i];

                                        //var customattributes = entity_property.GetCustomAttributes(false);
                                        //bool isprimarykey = customattributes.Any(a => a.GetType().Name == "KeyAttribute");
                                        //bool isvalidproperty = !customattributes.Any(a => a.GetType().Name == "NotMappedAttribute");

                                        //if (isvalidproperty)
                                        //{
                                        //if (isprimarykey)
                                        //{
                                        //    DatabaseGeneratedAttribute propdatabasegeneratednone = (DatabaseGeneratedAttribute)customattributes.FirstOrDefault(a => a.GetType().Name == "DatabaseGeneratedAttribute");
                                        //    if (propdatabasegeneratednone != null)
                                        //    {
                                        //        if (!validprimarykeys_options.Contains(propdatabasegeneratednone.DatabaseGeneratedOption))
                                        //        {
                                        //            isprimarykey = false;
                                        //        }
                                        //    }
                                        //}

                                        //if (!isprimarykey || !theyhavejustonekey)
                                        //{

                                        //var objvalue = ;
                                        var value = entity_property.GetValue(entity);
                                        if (entity_property.PropertyType == typeof(int) || entity_property.PropertyType == typeof(int?))
                                        {
                                            insertintovalue[i] = value == null ? "NULL" : (value + "").Replace(",", ".");
                                        }
                                        else if (entity_property.PropertyType == typeof(bool) || entity_property.PropertyType == typeof(bool?))
                                        {
                                            insertintovalue[i] = value == null ? "NULL" : value == "False" ? "0" : "1";
                                        }
                                        else if (entity_property.PropertyType == typeof(double) || entity_property.PropertyType == typeof(double?))
                                        {
                                            insertintovalue[i] = value == null ? "NULL" : (value + "").Replace(",", ".");
                                        }
                                        else if (entity_property.PropertyType == typeof(decimal) || entity_property.PropertyType == typeof(decimal?))
                                        {
                                            insertintovalue[i] = value == null ? "NULL" : (value + "").Replace(",", ".");
                                        }
                                        else if (entity_property.PropertyType == typeof(DateTime) || entity_property.PropertyType == typeof(DateTime?))
                                        {
                                            if (value == null)
                                            {
                                                insertintovalue[i] = "NULL";
                                            }
                                            else
                                            {
                                                insertintovalue[i] = "'" + string.Format("{0:yyyy-MM-dd hh:mm:ss}", Convert.ToDateTime(value)) + "'";

                                            }
                                        }
                                        else
                                        {
                                            insertintovalue[i] = value == null ? "NULL" : "'" + Extensoras.MySQLEscape(value + "") + "'";
                                        }
                                    }
                                    //    }
                                    //}
                                    insertintovalue[entity_properties.Length] = "'" + sqlinsert_uniquedt + "'";
                                    insertintovalue = insertintovalue.Where(a => a != null).ToArray();
                                    sqlinsert_values.Add("(" + string.Join(",", insertintovalue, 0, insertintovalue.Length) + ")");
                                }

                                if (!bd.ExisteCampo(tabletoinsert_name, sqlinsert_tempfieldname))
                                {
                                    bd.Database.ExecuteSqlCommand(string.Format("ALTER TABLE `{0}` ADD COLUMN `{1}` VARCHAR(255) NULL;", tabletoinsert_name, sqlinsert_tempfieldname));
                                    bd.Database.ExecuteSqlCommand(string.Format("ALTER TABLE `{0}` ADD INDEX `tempindex_bulk_unique_id` (`{1}`);", tabletoinsert_name, sqlinsert_tempfieldname));
                                }

                                string table_firstvalues = sqlinsert_values[0];
                                sqlinsert_values.RemoveAt(0);

                                string sqlinsert = string.Format("INSERT INTO {0} {1} VALUES {2}, {3};", 
                                    "`" + tabletoinsert_name + "`", 
                                    "(" + string.Join(",", sqlinsert_properties,0, sqlinsert_properties.Length) + ")", 
                                    table_firstvalues, 
                                    string.Join(",", sqlinsert_values));

                                int numrowinserted = bd.Database.ExecuteSqlCommand(sqlinsert);

                                if (sqlinsert_primarykeys.Count > 0)
                                {
                                    string sql_select = string.Format("SELECT CONCAT({0}) AS chaves FROM {1} WHERE {2} = {3}",
                                        string.Join(",", sqlinsert_primarykeys), 
                                        tabletoinsert_name, 
                                        sqlinsert_tempfieldname, 
                                        "'" + sqlinsert_uniquedt + "'");

                                    var primarykeys_inserted = bd.Database.SqlQuery<string>(sql_select).ToList();

                                    string[] newsqlinsert_primarykeys = sqlinsert_primarykeys.Where(a => a != "';'").ToArray();
                                    if (newsqlinsert_primarykeys.Length > 0)
                                    {
                                        for (int i = 0; i < primarykeys_inserted.Count; i++)
                                        {
                                            var entity = entities[i];
                                            var entity_type = entity.GetType();
                                            string[] values = primarykeys_inserted[i].Split(';');
                                            for (int i2 = 0; i2 < newsqlinsert_primarykeys.Length; i2++)
                                            {
                                                var value = values[i2];
                                                if (newsqlinsert_primarykeys[i2] != null)
                                                {
                                                    var entity_property = entity_type.GetProperty(newsqlinsert_primarykeys[i2]);
                                                    if (entity_property != null)
                                                    {
                                                        if (entity_property.PropertyType == typeof(int) || entity_property.PropertyType == typeof(int?))
                                                        {
                                                            entity_property.SetValue(entity, value == null ? (int?)null : Convert.ToInt32(value));
                                                        }
                                                        else if (entity_property.PropertyType == typeof(DateTime) || entity_property.PropertyType == typeof(DateTime?))
                                                        {
                                                            entity_property.SetValue(entity,
                                                                value == null ? (DateTime?)null : Convert.ToDateTime(value));
                                                        }
                                                        else if (nulltypes.Contains(entity_property.PropertyType))
                                                        {
                                                            entity_property.SetValue(entity, value == null ? null : value);
                                                        }
                                                        else if (entity_property.PropertyType == typeof(bool) || entity_property.PropertyType == typeof(bool?))
                                                        {
                                                            entity_property.SetValue(entity, value == null ? (bool?)null : Convert.ToInt32(value) == 1);
                                                        }
                                                        else if (entity_property.PropertyType == typeof(double) || entity_property.PropertyType == typeof(double?))
                                                        {
                                                            entity_property.SetValue(entity, value == null ? (double?)null : Convert.ToDouble(value));
                                                        }
                                                        else if (entity_property.PropertyType == typeof(decimal) || entity_property.PropertyType == typeof(decimal?))
                                                        {
                                                            entity_property.SetValue(entity, value == null ? (decimal?)null : Convert.ToDecimal(value));
                                                        }
                                                        else
                                                        {
                                                            entity_property.SetValue(entity,
                                                                value == null ? null : value);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }

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
                if (entities.Count < 100)
                {
                    int count = entities.Count - 1;
                    while (count != -1)
                    {
                        for (int i = 0; i < 10; i++, count--)
                        {
                            if (count == -1)
                                break;

                            var entity = entities[count];
                            bd.Entry(entity).State = EntityState.Modified;
                        }
                        bd.SaveChanges();

                    }
                    return;
                }

                string tabletemp_name = ("tmp_t_" + Guid.NewGuid()).Replace("-", "_");
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

                            DatabaseGeneratedOption[] validprimarykeys_options = new DatabaseGeneratedOption[]
                                                {
                                                    DatabaseGeneratedOption.Computed, 
                                                    DatabaseGeneratedOption.Identity, 
                                                };
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
                                    var customattributes = property.GetCustomAttributes(false);
                                    bool isprimarykey = customattributes.Any(a => a.GetType().Name == "KeyAttribute");
                                    if (isprimarykey)
                                    {
                                        DatabaseGeneratedAttribute propdatabasegeneratednone = (DatabaseGeneratedAttribute)customattributes.FirstOrDefault(a => a.GetType().Name == "DatabaseGeneratedAttribute");
                                        if (propdatabasegeneratednone != null)
                                        {
                                            if (!validprimarykeys_options.Contains(propdatabasegeneratednone.DatabaseGeneratedOption))
                                            {
                                                isprimarykey = false;
                                            }
                                        }
                                    }
                                    bool isforeingkey = customattributes.Any(a => a.GetType().Name == "ForeignKeyAttribute");
                                    bool isvalidproperty = !customattributes.Any(a => a.GetType().Name == "NotMappedAttribute");
                                    bool isintornullint = property.PropertyType == typeof(int) ||
                                                              property.PropertyType == typeof(int?);
                                    bool isvalidint = true;
                                    if (isprimarykey)
                                    {
                                        if (isintornullint)
                                        {
                                            var firstproperty = entities[0].GetType().GetProperty(property.Name);
                                            if (firstproperty != null)
                                            {
                                                var firstvalue = firstproperty.GetValue(entities[0]);
                                                isvalidint = ((firstvalue != null && (int)firstvalue != 0) || firstvalue == null);
                                            }

                                        }
                                    }

                                    if (!isforeingkey && isvalidproperty && (!isintornullint || isintornullint && isvalidint))
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

                                List<string> sqltemp_insertintovalues = new List<string>();
                                Type[] nulltypes = new Type[] { typeof(int?), typeof(DateTime?), typeof(bool?), typeof(decimal?), typeof(double?), typeof(TimeSpan?) };
                                foreach (var entity in entities)
                                {
                                    var entity_type = entity.GetType();
                                    var entity_properties = entity_type.GetProperties()
                                        .Where(a => sqltemp_insertintoproperties.Contains(a.Name))
                                        .OrderBy(a => a.CustomAttributes.Any(b => b.GetType().Name == "KeyAttribute")).ToArray();
                                    string[] insertintovalue = new string[entity_properties.Length];
                                    for (int i = 0; i < entity_properties.Length; i++)
                                    {
                                        var entity_property = entity_properties[i];
                                        var objvalue = entity_property.GetValue(entity);
                                        var value = objvalue != null ? Extensoras.MySQLEscape(objvalue + "") : null;
                                        //var customattributes = entity_property.GetCustomAttributes(false);
                                        //bool isprimarykey = entity_property.GetCustomAttributes(false).Any(a => a.GetType().Name == "KeyAttribute");
                                        //bool isvalidproperty = !customattributes.Any(a => a.GetType().Name == "NotMappedAttribute");
                                        //if (isvalidproperty)
                                        //{
                                        if (entity_property.PropertyType == typeof(int) || entity_property.PropertyType == typeof(int?))
                                        {
                                            insertintovalue[i] = value == null ? "NULL" : (value + "").Replace(",", ".");
                                        }
                                        else if (entity_property.PropertyType == typeof(bool) || entity_property.PropertyType == typeof(bool?))
                                        {
                                            insertintovalue[i] = value == null ? "NULL" : value == "False" ? "0" : "1";
                                        }
                                        else if (entity_property.PropertyType == typeof(double) || entity_property.PropertyType == typeof(double?))
                                        {
                                            insertintovalue[i] = value == null ? "NULL" : (value + "").Replace(",", ".");
                                        }
                                        else if (entity_property.PropertyType == typeof(decimal) || entity_property.PropertyType == typeof(decimal?))
                                        {
                                            insertintovalue[i] = value == null ? "NULL" : (value + "").Replace(",", ".");
                                        }
                                        else if (entity_property.PropertyType == typeof(DateTime) || entity_property.PropertyType == typeof(DateTime?))
                                        {
                                            if (value == null)
                                            {
                                                insertintovalue[i] = "NULL";
                                            }
                                            else
                                            {
                                                insertintovalue[i] = "'" + string.Format("{0:yyyy-MM-dd hh:mm:ss}", Convert.ToDateTime(value)) + "'";

                                            }
                                        }
                                        else
                                        {
                                            insertintovalue[i] = value == null ? "NULL" : "'" + Extensoras.MySQLEscape(value + "") + "'";
                                        }
                                    }

                                    //}
                                    insertintovalue = insertintovalue.Where(a => a != null).ToArray();
                                    sqltemp_insertintovalues.Add("(" + string.Join(",", insertintovalue) + ")");
                                }
                                sqltemp_insertintoproperties = sqltemp_insertintoproperties.Where(a => a != null).ToArray();
                                sqlupdate_setters = sqlupdate_setters.Where(a => a != null).ToArray();
                                sqlupdate_innerjoin_where = sqlupdate_innerjoin_where.Where(a => a != null).ToArray();
                                sqltemp_primarykeys = sqltemp_primarykeys.Where(a => a != null).ToArray();
                                sqltemp_properties = sqltemp_properties.Where(a => a != null).ToArray();

                                string stemptable_properties = string.Join(",", sqltemp_properties, 0, sqltemp_properties.Length).Trim();
                                string stemptable_keys = sqltemp_primarykeys.Length > 0 ? string.Join(",", sqltemp_primarykeys,0, sqltemp_primarykeys.Length) : "";

                                string sqltemptable = string.Format("CREATE TEMPORARY TABLE {0}" +
                                    "( {1}  {2} ) ENGINE=InnoDB DEFAULT CHARSET=utf8;", "`" + tabletemp_name + "`", stemptable_properties, 
                                    sqltemp_primarykeys.Length > 0 ? ", PRIMARY KEY (" + stemptable_keys + ")" : "");
                                bd.Database.ExecuteSqlCommand(sqltemptable);

                                string table_firstvalues = sqltemp_insertintovalues[0];
                                sqltemp_insertintovalues.RemoveAt(0);

                                string sqlinsert = string.Format("INSERT INTO {0} ({1}) VALUES {2}, {3};",
                                    "`" + tabletemp_name + "`",
                                    string.Join(",", sqltemp_insertintoproperties, 0, sqltemp_insertintoproperties.Length),
                                    table_firstvalues, string.Join(", ", sqltemp_insertintovalues));

                                bd.Database.ExecuteSqlCommand(sqlinsert);
                                temptable_created = true;

                                string sqlupdate = string.Format("UPDATE {0} AS a " +
                                                                "INNER JOIN {1} AS b ON {2}" +
                                                                " SET {3};", "`" + tabletoupdate_name + "`", 
                                                                "`" + tabletemp_name + "`", 
                                                                string.Join(" AND ", sqlupdate_innerjoin_where, 0, sqlupdate_innerjoin_where.Length), 
                                                                string.Join(",", sqlupdate_setters,0, sqlupdate_setters.Length));

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
