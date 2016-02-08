// Generic reflection style
RegisterTemplate( new SiteProjectionTemplate<SiteModel>( Register, PersistanceManagerFactory )
	.InitEventHandler<SiteCreatedEvent>()
		.WithId( e => e.Id, "Id", "SiteId" )
		.WithProperty( e => e.Id )
		.WithProperty( e => e.Name )
		.AsInsertAction( 
			buferred: true,
			flushStrategy: FlushStrategy
				.FlushOnAnyOther()
				.FlushOn( typeof( Event1 ), typeof( Event2 ) ) )
	.InitEventHandler<SiteDeletedEvent>()
		.WithId( e => e.Id, "Id", "SiteId" )
		.AsDeleteAction()
	.InitEventHandler<SiteUpdatedEvent>()
		.WithId( e => e.Id, "Id", "SiteId" )
		.WithProperty( e => e.Name )
		.AsUpdateAction(
			buferred: true ),
			flushStrategy: FlushStrategy
				.FlushOnAnyOther()
				.FlushOn( typeof( Event1 ), typeof( Event2 ) ) );

// Generic expression style
RegisterTemplate( new SiteProjectionTemplate<SiteModel>( Register, PersistanceManagerFactory )
	.InitInsertEventHandler<SiteCreatedEvent>(
		( db, msg ) => 
			new SiteModel
			{
				Id = msg.Id,
				Name = msg.Name
			},
		buferred: true,
		flushStrategy: FlushStrategy
			.FlushOnAnyOther()
			.FlushOn( typeof( Event1 ), typeof( Event2 ) ) )
	.InitDeleteEventHandler<SiteDeletedEvent>( ( db, msg ) => site => site.Id == msg.Id )
	.InitUpdateEventHandler<SiteUpdatedEvent>( 
		( db, msg ) => site => site.Id == msg.Id,
		( db, msg ) => 
			site => 
			new SiteModel
			{ 			
				Name = msg.Name,
				Title = db.Single<OtherReadModel>( m => m.Id == msg.OtherId ).Title
			},
		buferred: true,
		flushStrategy: FlushStrategy
			.FlushOnAnyOther()
			.FlushOn( typeof( Event1 ), typeof( Event2 ) ) )

// Mixed Generic style
public class SiteProjectionTemplate
{
	public SiteProjectionTemplate( ... )
	{
		InitEventHandler<SiteCreatedEvent>()
			.WithId( e => e.Id, "Id", "SiteId" )
			.WithProperty( e => e.Id )
			.WithProperty( e => e.Name )
			.AsInsertAction( 
				buferred: true,
				flushStrategy: FlushStrategy
					.FlushOnAnyOther()
					.FlushOn( typeof( Event1 ), typeof( Event2 ) ) );
	
		InitEventHandler<SiteDeletedEvent>()
			.WithId( e => e.Id, "Id", "SiteId" )
			.AsDeleteAction();
	
		InitEventHandler<SiteUpdatedEvent>()
			.WithId( e => e.Id, "Id", "SiteId" )
			.WithProperty( e => e.Name )
			.AsUpdateAction(
				buferred: true ),
				flushStrategy: FlushStrategy
					.FlushOnAnyOther()
					.FlushOn( typeof( Event1 ), typeof( Event2 ) ) );
	}
}

RegisterTemplate( new SiteProjectionTemplate<SiteModel>( Register, PersistanceManagerFactory )
 	.CustomiseHandler<SiteAssignedToContentGeneratorEvent>(
 		updateWith: 
        	( db, msg ) =>
        	{
            	var api = db.Single<SiteModelApiVersion>( v => v.Id == msg.ApiVersionId );
            	return site => new SiteModel
            	{
                	ApiVersionId = api.Id,
                	ApiVersionLowLevel = api.LowLevelVersion,
                	ApiVersionHighLevel = api.HighLevelVersion
            	};
        	},
        // todo: what should we do if validation is failed - throw exception, or just skip the event?
        //  make a list of real cases
        validateWith: ( db, msg ) => { return msg.Name.Contains( "required part" ); },
        runBeforeHandle: ( db, msg ) => { someEvent.Execute( new EventArg() ); } ) );
        runAfterHandle: ( db, msg ) => {  someEvent.Execute( new EventArg() ); } ) );


RegisterTemplate( new SiteProjectionTemplate<SiteModel>( Register, PersistanceManagerFactory )
 	.UpdateWith<SiteAssignedToContentGeneratorEvent>( 
 		( db, msg ) =>
       	{
           	var api = db.Single<SiteModelApiVersion>( v => v.Id == msg.ApiVersionId );
           	return site => new SiteModel
           	{
               	ApiVersionLowLevel = api.LowLevelVersion,
               	ApiVersionHighLevel = api.HighLevelVersion
           	};
       	} )
 	.ValidateWith<SiteAssignedToContentGeneratorEvent>( ( db, msg ) => msg.Name.Contains( "required part" ) )
 	.RunBeforeHandle<SiteAssignedToContentGeneratorEvent>( ( db, msg ) => { someEvent.Execute( new EventArg() ); } ) )
 	.RunAfterHandle<SiteAssignedToContentGeneratorEvent>( ( db, msg ) => { someEvent.Execute( new EventArg() ); } ) );



