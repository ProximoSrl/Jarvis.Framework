using Castle.Core.Logging;
using Castle.Windsor;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Commands.Tracking;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Messages;
using Newtonsoft.Json;
using Rebus.Bus;
using Rebus.Messages;
using Rebus.Retry;
using Rebus.Transport;
using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Rebus.Support
{
    public partial class BusBootstrapper
    {
        private class JarvisFrameworkErrorHandler : IErrorHandler
        {
            private const string JsonContentTypeName = "application/json";
            private readonly JsonSerializerSettings _jsonSerializerSettings;
            private readonly IWindsorContainer _container;

            private readonly Lazy<IBus> _lazyBus;
            private readonly Lazy<IMessagesTracker> _lazyMessageTracker;
            private readonly ILogger _logger;
            private ITransport _transport;
            private readonly JarvisRebusConfiguration _jarvisRebusConfiguration;

            public JarvisFrameworkErrorHandler(
                JarvisRebusConfiguration jarvisRebusConfiguration,
                JsonSerializerSettings jsonSerializerSettings,
                IWindsorContainer container,
                ILogger logger)
            {
                _jsonSerializerSettings = jsonSerializerSettings;
                _container = container;
                _lazyBus = new Lazy<IBus>(() => _container.Resolve<IBus>());
                _lazyMessageTracker = new Lazy<IMessagesTracker>(() => _container.Resolve<IMessagesTracker>());
                _logger = logger;
                _jarvisRebusConfiguration = jarvisRebusConfiguration;
            }

            public void SetTransport(ITransport transport)
            {
                _transport = transport;
                //Create the error queue, can ignore any other error because in this phase rebus is
                //initializing and throwing in this routine can generate a strange an difficult to diagnostic
                //error message.
                try
                {
                    _transport.CreateQueue(_jarvisRebusConfiguration.ErrorQueue);
                }
                catch (Exception ex)
                {
                    _logger.ErrorFormat(ex, "Error creating error queue {0} - {1}", _jarvisRebusConfiguration.ErrorQueue, ex.Message);
                }
            }

            public async Task HandlePoisonMessage(TransportMessage transportMessage, ITransactionContext transactionContext, Exception exception)
            {
                try
                {
                    if (transportMessage.Headers.ContainsKey("rbs2-msg-id"))
                    {
                        Guid commandId = Guid.Parse(transportMessage.Headers["rbs2-msg-id"]);
                        var description = transportMessage.Headers["rbs2-msg-type"];
                        String exMessage = description;

                        while (exception is TargetInvocationException)
                        {
                            exception = exception.InnerException;
                        }

                        var command = GetCommandFromMessage(transportMessage);
                        if (command != null)
                        {
                            //Ok we need to track failure of the command, but only if this message is a command.
                            _lazyMessageTracker.Value.Failed(command, DateTime.UtcNow, exception);
                        }

                        if (exception != null)
                        {
                            exMessage = GetErrorMessage(exception);
                            _logger.ErrorFormat("HandlingPoisionMessage for {0}/{1} - {2}", commandId, description, command?.Describe());
                        }

                        if (command != null)
                        {
                            var notifyTo = command.GetContextData(MessagesConstants.ReplyToHeader);

                            if (!string.IsNullOrEmpty(notifyTo))
                            {
                                var commandHandled = new CommandHandled(
                                        notifyTo,
                                        commandId,
                                        CommandHandled.CommandResult.Failed,
                                        description,
                                        exMessage
                                        );

                                commandHandled.CopyHeaders(command);

                                Dictionary<String, String> headers = new Dictionary<string, string>
                                {
                                    { Headers.MessageId, Guid.NewGuid().ToString() }
                                };

                                //TODO: WIth new rebus I do not know how to resend header back. This will throw some unknown and obscure error in rebus.
                                await _lazyBus.Value.Advanced.Routing.Send(
                                   transportMessage.Headers["rbs2-return-address"],
                                   commandHandled,
                                   headers).ConfigureAwait(false);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error during HandlePoisonMessage of message: {ex.Message}", ex);
                }
                finally
                {
                    var headers = transportMessage.Headers;
                    headers[Headers.ErrorDetails] = exception?.ToString();
                    headers[Headers.SourceQueue] = _transport.Address;
                    _logger.Error($"Moving message to error queue {_jarvisRebusConfiguration.ErrorQueue}", exception);
                    if (_transport == null)
                    {
                        _logger.Error("Error handler has no transport...this should be not possible. Problem in rebus initialization");
                    }
                    else
                    {
                        await _transport.Send(_jarvisRebusConfiguration.ErrorQueue, transportMessage, transactionContext).ConfigureAwait(false);
                    }
                }
            }

            private static string GetErrorMessage(Exception exception)
            {
                if (exception is AggregateException aggEx)
                {
                    StringBuilder errorMessage = new StringBuilder();
                    errorMessage.AppendLine($"We have a total of {aggEx.InnerExceptions.Count} exceptions");
                    foreach (var e in aggEx.InnerExceptions)
                    {
                        errorMessage.AppendLine(e.GetExceptionDescription());
                        errorMessage.AppendLine("\n\n-------------------------------------------------------------\n\n");
                    }
                    return errorMessage.ToString();
                }
                return exception.Message;
            }

            private ICommand GetCommandFromMessage(TransportMessage message)
            {
                var deserializedMessage = Deserialize(message);

                if (deserializedMessage == null)
                    return null;

                //check if the body is a command
                if (deserializedMessage.Body is ICommand cmd)
                {
                    return cmd;
                }

                //can be an array of messages, takes the first one
                if ((deserializedMessage.Body as Object[])?.Length > 0)
                {
                    var command = (deserializedMessage.Body as Object[])?[0] as ICommand;
                    if (command != null) return command;
                }

                //This is not a command.
                return null;

#pragma warning disable S125
                // Sections of code should not be "commented out"
                //string body;
                //switch (message.Headers["rebus-encoding"].ToLowerInvariant())
                //{
                //	case "utf-7":
                //		body = Encoding.UTF7.GetString(message.Body);
                //		break;
                //	case "utf-8":
                //		body = Encoding.UTF8.GetString(message.Body);
                //		break;
                //	case "utf-32":
                //		body = Encoding.UTF32.GetString(message.Body);
                //		break;
                //	case "ascii":
                //		body = Encoding.ASCII.GetString(message.Body);
                //		break;
                //	case "unicode":
                //		body = Encoding.Unicode.GetString(message.Body);
                //		break;
                //	default:
                //		return null;
                //}

                //var msg = JsonConvert.DeserializeObject(body, _jsonSerializerSettings);
                //var array = msg as Object[];
                //if (array != null)
                //{
                //	return array[0] as ICommand;
                //}

                //return null;
            }
#pragma warning restore S125 // Sections of code should not be "commented out"

            private Message Deserialize(TransportMessage transportMessage)
            {
                var headers = new Dictionary<String, String>(transportMessage.Headers);
                var encodingToUse = GetEncodingOrThrow(headers);

                var serializedTransportMessage = encodingToUse.GetString(transportMessage.Body);
                try
                {
                    Object deserialized = JsonConvert.DeserializeObject(serializedTransportMessage, _jsonSerializerSettings);
                    return new Message(headers, deserialized);
                }
                catch (Exception e)
                {
                    throw new ArgumentException(
                        string.Format(
                            "An error occurred while attempting to deserialize JSON text '{0}' into an object[]",
                            serializedTransportMessage), e);
                }
            }

            private Encoding GetEncodingOrThrow(IDictionary<string, String> headers)
            {
                if (!headers.ContainsKey(Headers.ContentType))
                {
                    throw new ArgumentException(
                        string.Format("Received message does not have a proper '{0}' header defined!",
                                      Headers.ContentType));
                }

                var contentTypeHeaderValue = (headers[Headers.ContentType] ?? "");
                var contentType = new ContentType(contentTypeHeaderValue);

                if (!contentType.MediaType.StartsWith(JsonContentTypeName, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new ArgumentException(
                        string.Format(
                            "Received message has content type header with '{0}' which is not supported by the JSON serializer!",
                            contentTypeHeaderValue));
                }

                if (String.IsNullOrWhiteSpace(contentType.CharSet))
                {
                    throw new ArgumentException(
                        string.Format(
                            "Received message has content type '{0}', but it has no charset defined!",
                            contentTypeHeaderValue));
                }

                try
                {
                    return Encoding.GetEncoding(contentType.CharSet);
                }
                catch (Exception e)
                {
                    throw new ArgumentException(
                        string.Format("An error occurred while attempting to treat '{0}' as a proper text encoding",
                                      contentType.CharSet), e);
                }
            }
        }
    }
}
