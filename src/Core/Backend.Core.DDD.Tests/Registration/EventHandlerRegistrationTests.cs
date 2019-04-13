﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GoldenEye.Backend.Core.DDD.Events;
using GoldenEye.Backend.Core.DDD.Registration;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Backend.Core.DDD.Tests.Registration
{
    public class EventHandlerRegistrationTests
    {
        public class UserCreated : IEvent
        {
            public Guid UserId { get; }
            public Guid StreamId => UserId;

            public UserCreated(Guid userId)
            {
                UserId = userId;
            }
        }

        public class UsersCountHandler : IEventHandler<UserCreated>
        {
            public int UserCount { get; private set; }

            public Task Handle(UserCreated @event, CancellationToken cancellationToken)
            {
                UserCount++;
                return Unit.Task;
            }
        }

        public class UsersIdsHandler : IEventHandler<UserCreated>
        {
            public List<Guid> UserIds { get; private set; } = new List<Guid>();

            public Task Handle(UserCreated @event, CancellationToken cancellationToken)
            {
                UserIds.Add(@event.StreamId);
                return Unit.Task;
            }
        }

        [Fact]
        public async Task GivenTwoEventHandlers_WhenEventIsPublished_ThenBothHandles()
        {
            //Given
            var services = new ServiceCollection();
            services.AddDDD();
            services.RegisterEventHandler<UserCreated, UsersCountHandler>(ServiceLifetime.Singleton);
            services.RegisterEventHandler<UserCreated, UsersIdsHandler>(ServiceLifetime.Singleton);

            var sp = services.BuildServiceProvider();
            var eventBus = sp.GetService<IEventBus>();
            var @event = new UserCreated(Guid.NewGuid());

            //When
            await eventBus.PublishAsync(@event);

            //Then
            var usersCountHandler = sp.GetService<UsersCountHandler>();
            usersCountHandler.UserCount.Should().Be(1);

            var usersIdsHandler = sp.GetService<UsersIdsHandler>();
            usersIdsHandler.UserIds.Should().HaveCount(1);
            usersIdsHandler.UserIds.Should().Contain(@event.UserId);
        }
    }

    public class EventHandlerAllRegistrationTests
    {
        public class UserAdded : IEvent
        {
            public Guid StreamId => Guid.Empty;
        }

        public class UserUpdated : IEvent
        {
            public Guid StreamId => Guid.Empty;
        }

        public class AccountAdded : IEvent
        {
            public Guid StreamId => Guid.Empty;
        }

        public class AccountUpdated : IEvent
        {
            public Guid StreamId => Guid.Empty;
        }

        public class AccountDeleted : IEvent
        {
            public Guid StreamId => Guid.Empty;
        }

        public class UserEventHandler :
            IEventHandler<UserAdded>,
            IEventHandler<UserUpdated>
        {
            public Task Handle(UserAdded request, CancellationToken cancellationToken)
            {
                return Unit.Task;
            }

            public Task Handle(UserUpdated request, CancellationToken cancellationToken)
            {
                return Unit.Task;
            }
        }

        public abstract class BaseAccountEventHandler :
            IEventHandler<AccountAdded>,
            IEventHandler<AccountUpdated>
        {
            public abstract Task Handle(AccountAdded request, CancellationToken cancellationToken);

            public Task Handle(AccountUpdated request, CancellationToken cancellationToken)
            {
                return Unit.Task;
            }
        }

        public class AccountEventHandler :
            BaseAccountEventHandler,
            IEventHandler<AccountDeleted>
        {
            public override Task Handle(AccountAdded request, CancellationToken cancellationToken)
            {
                return Unit.Task;
            }

            public Task Handle(AccountDeleted request, CancellationToken cancellationToken)
            {
                return Unit.Task;
            }
        }

        public class DuplicatedDeleteAccountEventHandler :
            IEventHandler<AccountDeleted>
        {
            public Task Handle(AccountDeleted request, CancellationToken cancellationToken)
            {
                return Unit.Task;
            }
        }

        private ServiceCollection services = new ServiceCollection();

        public EventHandlerAllRegistrationTests()
        {
            services.AddAllEventHandlers(ServiceLifetime.Scoped);
        }

        [Fact]
        public void GivenMultipleEventHandler_WhenAddAllEventHandlerCalled_ThenAllEventHandlersAreRegistered()
        {
            using (var sp = services.BuildServiceProvider())
            {
                var addUserHandlers = sp.GetServices<INotificationHandler<UserAdded>>()
                    .Union(sp.GetServices<IEventHandler<UserAdded>>()).ToList();
                var updateUserHandlers = sp.GetServices<INotificationHandler<UserUpdated>>()
                    .Union(sp.GetServices<IEventHandler<UserUpdated>>()).ToList();

                addUserHandlers.Should().ContainSingle();
                addUserHandlers.Should().AllBeOfType<UserEventHandler>();

                updateUserHandlers.Should().ContainSingle();
                updateUserHandlers.Should().AllBeOfType<UserEventHandler>();
            }
        }

        [Fact]
        public void GivenBaseEventHandler_WhenAddAllEventHandlerCalled_ThenOnlyDerivedClassIsRegistered()
        {
            using (var sp = services.BuildServiceProvider())
            {
                var addAccountHandlers = sp.GetServices<INotificationHandler<AccountAdded>>()
                    .Union(sp.GetServices<IEventHandler<AccountAdded>>());
                var updateAccountHandlers = sp.GetServices<INotificationHandler<AccountUpdated>>()
                    .Union(sp.GetServices<IEventHandler<AccountUpdated>>());

                addAccountHandlers.Should().ContainSingle();
                addAccountHandlers.Should().AllBeOfType<AccountEventHandler>();

                updateAccountHandlers.Should().ContainSingle();
                updateAccountHandlers.Should().AllBeOfType<AccountEventHandler>();
            }
        }

        [Fact]
        public void GivenDuplicatedEventHandler_WhenAddAllEventHandlerCalled_ThenBothAreRegistered()
        {
            using (var sp = services.BuildServiceProvider())
            {
                var deleteAccountHandlers = sp.GetServices<INotificationHandler<AccountDeleted>>()
                    .Union(sp.GetServices<IEventHandler<AccountDeleted>>());

                deleteAccountHandlers.Should().HaveCount(2);
                deleteAccountHandlers.Should().Contain(x => x is AccountEventHandler);
                deleteAccountHandlers.Should().Contain(x => x is DuplicatedDeleteAccountEventHandler);
            }
        }
    }
}