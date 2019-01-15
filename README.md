This repository contains two methods that allow you to execute BulkInsert and BulkUpdate using MySql and EF6+. These are simple but functional functions, we also use transaction on both methods.

Entity Framework is a great tool when talking about a short pack of informations but when we grow up on it, it's become slow and does not fulfill our goals anymore. 

How does Entity Framework save the data when we do a DbSet<T>.AddRange(List<T>) or DbSet<T>.Add(T)?
- Basically, he creates a "INSERT INTO table (values)" and if you have a table with an AUTO_INCREMENT column he'll also execute a command (using MySql in my case) like "
SELECT LAST_INSERT_ID()" to fulfill your object.
- The same happens to DbSet<T>.Entry(T).State = EntityState.Modified or DbSet<T>.Attach(T) or any change that you decide to make.
- Suming up, he does a sql statement for each record and it's become very slow when we have a lot of data.

So, how does the method sort out this trouble?
- First thing first thing, i'm say we continuos to use Entity Framework but the main difference it's the way we chooce to save the data.
- On the BulkInsert Method we read all the objects passed (whom usually it's a model) and then create only one SQL statement which does a compatible Bulk Operation in MySql (my case). We also create a column called "bulk_unique_id" and an unique Guid to insert into this column, when the rows are inserted we select all those rows again into a separed "SELECT CONCAT" statement (in my case) and then fulfill the objects passed.
- On the BulkUpdate Method not only do we create an unique guid but also a temp table to insert our values and then make an update with INNER JOIN which is faster than update by row to row.

This has been intended developed to my needs and the project is opened to who concerns to improve it or change it for a better solution that will worth to the community.
