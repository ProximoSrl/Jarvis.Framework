#Custom mongo serialization

###Flat mapper

All class that inherits from IIDentity should be serialized as simple string, this allows for a cleaner storage of records inside mongo, avoiding the need to store an id as an object with _t to identify type.

To allow for flat mapping you **must call as first as possible** the following function.

	MongoFlatMapper.EnableFlatMapping();

This will register all flat serializer for you

###Custom mapping for messages and Domain Events

All messages (ex commands) and Domain Events use a custom class mapping called *AliasClassMap* that basically manage discriminator for serialization. If a class ends with Command or Event that suffix was stripped and a version is added taken from *VersionInfoAttribute*.

In the end what happen is, if you have DeleteDocumentCommand, and it has no *VersionInfoAttribute* the discriminator in mongo will be *DeleteDocument_1*.

Immediately after you enable flat mapping **but absolutely not before** you usually should register all assemblies that contains messages with a code like this. 

	MessagesRegistration.RegisterAssembly(typeof(AuthSharedDefinition).Assembly);
	MessagesRegistration.RegisterAssembly(typeof(DMSSharedDefinition).Assembly);
	MessagesRegistration.RegisterAssembly(typeof(ToolsSharedDefinition).Assembly);

Calling **RegisterAssembly** will register not only messages, but also Domain Event and everything that implements *ICommand interface*.

###Typical error you got if you not configure correctly custom serializers

Typical errors are "Unknown discriminator" where you get some document in mongo with a discriminator like DeleteDocument while the reader is expecting DeleteDocument_1. This happens if some component of the software forget to register the serialziers and save in mongo with different serialization.

Another typical error is when you got error when serializer is calling GetString while the record is a document. As an example, instead of finding a property like this in an object

	"DocumentId" : "Document_2"

you find something like

	"DocumentId" : {
		"Id" : "Document_2"
	}

This happens if **you forget to call MongoFlatMapper or if you call RegisterAssembly before MongoFlatMapper** remember that call to MongoFlatMapper **should be the very first instruction of your executable or web site**.