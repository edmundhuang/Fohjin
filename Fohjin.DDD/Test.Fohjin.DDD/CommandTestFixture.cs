using System;
using System.Collections.Generic;
using System.Linq;
using Fohjin.DDD.Bus;
using Fohjin.DDD.CommandHandlers;
using Fohjin.DDD.Commands;
using Fohjin.DDD.Contracts;
using Fohjin.DDD.Domain;
using Fohjin.DDD.Domain.Mementos;
using Fohjin.DDD.Events;
using Moq;

namespace Test.Fohjin.DDD
{
    [Specification]
    public abstract class CommandTestFixture<TCommand, TCommandHandler, TAggregateRoot> 
        where TCommand : class, ICommand, IMessage
        where TCommandHandler : class, ICommandHandler<TCommand>
        where TAggregateRoot : IOrginator, IEventProvider, new()
    {
        private IDictionary<Type, object> mocks;

        protected TAggregateRoot AggregateRoot;
        protected ICommandHandler<TCommand> CommandHandler;
        protected Exception CaughtException;
        protected IEnumerable<IDomainEvent> PublishedEvents;
        protected virtual void SetupDependencies() { }
        protected virtual IEnumerable<IDomainEvent> Given() 
        {
            return new List<IDomainEvent>();
        }
        protected virtual void Finally() { }
        protected abstract TCommand When();

        [Given]
        public void Setup()
        {
            mocks = new Dictionary<Type, object>();
            CaughtException = new ThereWasNoExceptionButOneWasExpectedException();
            AggregateRoot = new TAggregateRoot();
            AggregateRoot.LoadFromHistory(Given());

            CommandHandler = BuildCommandHandler();

            SetupDependencies();
            try
            {
                CommandHandler.Execute(When());
                PublishedEvents = AggregateRoot.GetChanges();
            }
            catch (Exception exception)
            {
                CaughtException = exception;
            }
            finally
            {
                Finally();
            }
        }

        public Mock<TType> OnDependency<TType>() where TType : class
        {
            return (Mock<TType>)mocks[typeof(TType)];
        }

        private ICommandHandler<TCommand> BuildCommandHandler()
        {
            var constructorInfo = typeof(TCommandHandler).GetConstructors().First();

            foreach (var parameter in constructorInfo.GetParameters())
            {
                if (parameter.ParameterType == typeof(IDomainRepository))
                {
                    var repositoryMock = new Mock<IDomainRepository>();
                    repositoryMock.Setup(x => x.GetById<TAggregateRoot>(It.IsAny<Guid>())).Returns(AggregateRoot);
                    repositoryMock.Setup(x => x.Save(It.IsAny<TAggregateRoot>())).Callback<TAggregateRoot>(x => AggregateRoot = x);
                    mocks.Add(parameter.ParameterType, repositoryMock);
                    continue;
                }

                mocks.Add(parameter.ParameterType, CreateMock(parameter.ParameterType));
            }

            return (ICommandHandler<TCommand>)constructorInfo.Invoke(mocks.Values.Select(x => ((Mock)x).Object).ToArray());
        }

        private static object CreateMock(Type type)
        {
            var constructorInfo = typeof (Mock<>).MakeGenericType(type).GetConstructors().First();
            return constructorInfo.Invoke(new object[]{});
        }
    }

    public class ThereWasNoExceptionButOneWasExpectedException : Exception {}

    public class PrepareDomainEvent
    {
        public static EventVersionSetter Set(IDomainEvent domainEvent)
        {
            return new EventVersionSetter(domainEvent);
        }
    }

    public class EventVersionSetter
    {
        private readonly IDomainEvent _domainEvent;

        public EventVersionSetter(IDomainEvent domainEvent)
        {
            _domainEvent = domainEvent;
        }

        public IDomainEvent ToVersion(int version)
        {
            _domainEvent.Version = version;
            return _domainEvent;
        }
    }
}