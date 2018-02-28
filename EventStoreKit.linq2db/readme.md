most of projections is an intersection of several data source, so we strongly need such DB features like:
        
        - batch update, i.e. when projection receives "UserRenamedEvent" it need to update user name all for all stored items as linq expression, convertible to single sql command
        - batch delete, 
        - batch insert: "get all records, which matches conditions from table1, convert to ReadModel2 and insert to table2" as linq expression, convertible to single SQL command
        - bulk insert - strongly need for projection template cached insert
        - powerfull linq expression builder, which allow to use all linq, and it will be compiled to SQL ( and it should be guaranteed, than it will be single SQL command for single linq expression )