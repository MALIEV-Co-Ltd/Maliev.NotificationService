using System.Linq.Expressions;
using Maliev.NotificationService.Api.Services;
using Maliev.NotificationService.Domain.Entities;
using Maliev.NotificationService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.NotificationService.Api.Tests.Unit.Services;

public class DeduplicationServiceTests
{
    private static (NotificationDbContext DbContext, List<DeduplicationEntry> Entries) CreateMockDbContext()
    {
        var entries = new List<DeduplicationEntry>();
        var mockSet = new Mock<DbSet<DeduplicationEntry>>();

        mockSet.As<IAsyncEnumerable<DeduplicationEntry>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(new TestAsyncEnumerator<DeduplicationEntry>(entries.GetEnumerator()));

        mockSet.As<IQueryable<DeduplicationEntry>>()
            .Setup(m => m.Provider)
            .Returns(new TestAsyncQueryProvider<DeduplicationEntry>(entries.AsQueryable().Provider));

        mockSet.As<IQueryable<DeduplicationEntry>>().Setup(m => m.Expression).Returns(entries.AsQueryable().Expression);
        mockSet.As<IQueryable<DeduplicationEntry>>().Setup(m => m.ElementType).Returns(entries.AsQueryable().ElementType);
        mockSet.As<IQueryable<DeduplicationEntry>>().Setup(m => m.GetEnumerator()).Returns(() => entries.GetEnumerator());

        mockSet.Setup(d => d.Add(It.IsAny<DeduplicationEntry>())).Callback<DeduplicationEntry>(e =>
        {
            if (entries.Any(x => x.EventId == e.EventId))
                throw new DbUpdateException("duplicate key value violates unique constraint", new Exception("unique"));
            entries.Add(e);
        });

        mockSet.Setup(d => d.Remove(It.IsAny<DeduplicationEntry>())).Callback<DeduplicationEntry>(e =>
        {
            entries.Remove(e);
        });

        var dbContextMock = new Mock<NotificationDbContext>(new DbContextOptions<NotificationDbContext>());
        dbContextMock.Setup(d => d.DeduplicationEntries).Returns(mockSet.Object);
        dbContextMock.Setup(d => d.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        return (dbContextMock.Object, entries);
    }

    private static DeduplicationService CreateService(
        NotificationDbContext dbContext,
        Mock<IDistributedCache>? mockCache = null)
    {
        return new DeduplicationService(
            (mockCache ?? new Mock<IDistributedCache>()).Object,
            dbContext,
            new Mock<ILogger<DeduplicationService>>().Object);
    }

    [Fact]
    public async Task IsDuplicateAsync_NewEvent_ShouldReturnFalse()
    {
        var mockCache = new Mock<IDistributedCache>();
        mockCache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);
        var (dbContext, _) = CreateMockDbContext();
        var service = CreateService(dbContext, mockCache);

        var result = await service.IsDuplicateAsync(Guid.NewGuid().ToString(), DateTimeOffset.UtcNow);

        Assert.False(result);
    }

    [Fact]
    public async Task IsDuplicateAsync_ExistingEventViaCache_ShouldReturnTrue()
    {
        var mockCache = new Mock<IDistributedCache>();
        mockCache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes("1"));
        var (dbContext, _) = CreateMockDbContext();
        var service = CreateService(dbContext, mockCache);

        var result = await service.IsDuplicateAsync(Guid.NewGuid().ToString(), DateTimeOffset.UtcNow);

        Assert.True(result);
    }

    [Fact]
    public async Task IsDuplicateAsync_ExistingEventViaDb_ShouldReturnTrue()
    {
        var mockCache = new Mock<IDistributedCache>();
        mockCache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);
        var (dbContext, _) = CreateMockDbContext();
        var service = CreateService(dbContext, mockCache);
        var eventId = Guid.NewGuid().ToString();
        var timestamp = DateTimeOffset.UtcNow;

        await service.IsDuplicateAsync(eventId, timestamp);
        var result = await service.IsDuplicateAsync(eventId, timestamp);

        Assert.True(result);
    }

    [Fact]
    public async Task IsDuplicateAsync_CacheException_FallsThroughToDb_ReturnsFalse()
    {
        var mockCache = new Mock<IDistributedCache>();
        mockCache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis connection failed"));
        var (dbContext, _) = CreateMockDbContext();
        var service = CreateService(dbContext, mockCache);

        var result = await service.IsDuplicateAsync(Guid.NewGuid().ToString(), DateTimeOffset.UtcNow);

        Assert.False(result);
    }

    [Fact]
    public async Task ClearCacheEntryAsync_RemovesFromCache()
    {
        var mockCache = new Mock<IDistributedCache>();
        var (dbContext, _) = CreateMockDbContext();
        var service = CreateService(dbContext, mockCache);

        await service.ClearCacheEntryAsync(Guid.NewGuid().ToString(), DateTimeOffset.UtcNow);

        mockCache.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class TestAsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
    {
        public T Current => inner.Current;
        public ValueTask DisposeAsync() { inner.Dispose(); return default; }
        public ValueTask<bool> MoveNextAsync() => new(inner.MoveNext());
    }

    private sealed class TestAsyncQueryProvider<T> : IAsyncQueryProvider
    {
        private readonly IQueryProvider _inner;

        public TestAsyncQueryProvider(IQueryProvider inner)
        {
            _inner = inner;
        }

        public IQueryable CreateQuery(Expression expression) => new TestAsyncEnumerable<T>(expression);
        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new TestAsyncEnumerable<TElement>(expression);
        public object? Execute(Expression expression) => _inner.Execute(expression);
        public TResult Execute<TResult>(Expression expression) => _inner.Execute<TResult>(expression);

        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
        {
            var resultType = typeof(TResult);
            if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var elementType = resultType.GetGenericArguments()[0];
                var syncResult = _inner.Execute(expression);
                var fromResultMethod = typeof(Task).GetMethod(nameof(Task.FromResult))!
                    .MakeGenericMethod(elementType);
                return (TResult)fromResultMethod.Invoke(null, [syncResult])!;
            }

            return (TResult)_inner.Execute(expression)!;
        }
    }

    private sealed class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }
        public TestAsyncEnumerable(Expression expression) : base(expression) { }
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
        IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
    }
}
