﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EventStoreKit.Sql.PersistanceManager;
using EventStoreKit.Utility;
using log4net;

namespace EventStoreKit.Sql.ProjectionTemplates
{
    public interface IDbStrategy<TReadModel> where TReadModel : class
    {
        void Flush();
        void Insert( Guid id, TReadModel readModel );
        void Update( Guid id, Expression<Func<TReadModel,bool>> predicat, ObjectExpressionBuilder<TReadModel> expressionBuilder );
    }

    internal class DbStrategyDirect<TReadModel> : IDbStrategy<TReadModel> where TReadModel : class
    {
        private readonly Func<IDbProvider> DbProviderFactory;

        public DbStrategyDirect( Func<IDbProvider> dbProviderFactory )
        {
            DbProviderFactory = dbProviderFactory;
        }

        public void Flush(){}

        public void Insert( Guid id, TReadModel readModel )
        {
            DbProviderFactory.Run( db => db.Insert( readModel ) );
        }

        public void Update( Guid id, Expression<Func<TReadModel, bool>> predicat, ObjectExpressionBuilder<TReadModel> expressionBuilder )
        {
            DbProviderFactory.Run( db => db.Update( predicat, expressionBuilder.GenerateUpdatExpression( false ) ) );
        }
    }

    internal class DbStrategyBuffered<TReadModel> : IDbStrategy<TReadModel> where TReadModel : class
    {
        private readonly Func<IDbProvider> DbProviderFactory;
        private readonly ILog Logger;
        private readonly int BufferCount;

        private readonly Dictionary<Guid, TReadModel> Buffer = new Dictionary<Guid, TReadModel>();

        public DbStrategyBuffered( Func<IDbProvider> dbProviderFactory, ILog logger, int bufferCount )
        {
            DbProviderFactory = dbProviderFactory;
            Logger = logger;
            BufferCount = bufferCount;
        }

        public void Flush()
        {
            if ( Buffer.Any() )
            {
                var count = Buffer.Count();
                DbProviderFactory.Run( db => db.InsertBulk( Buffer.Values ) );
                Buffer.Clear();
                Logger.Do( log => log.ErrorFormat( "Bulk record inserted, count = {0}", count ) );
                //Logger.Do( log => log.InfoFormat( "Bulk record inserted, count = {0}", count ) );
            }
        }

        public void Insert( Guid id, TReadModel readModel )
        {
            Buffer.Add( id, readModel );
            if ( Buffer.Count() >= BufferCount )
            {
                Flush();
            }
        }

        public void Update( Guid id, Expression<Func<TReadModel, bool>> predicat, ObjectExpressionBuilder<TReadModel> expressionBuilder )
        {
            if ( Buffer.ContainsKey( id ) )
            {
                var evaluator = expressionBuilder.GenerateUpdatExpression( true );
                Buffer[id] = evaluator.Compile()( Buffer[id] );
            }
            else
            {
                var evaluator = expressionBuilder.GenerateUpdatExpression( false );
                DbProviderFactory.Run( db => db.Update( predicat, evaluator ) );
            }
        }
    }
}