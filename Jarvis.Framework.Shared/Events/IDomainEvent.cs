using Jarvis.Framework.Shared.Messages;

namespace Jarvis.Framework.Shared.Events
{
	public interface IDomainEvent : IMessage
    {
	    /// <summary>
	    /// Identificativo dell'utente che ha scatenato l'evento
	    /// </summary>
	    string IssuedBy { get; }
    }
}