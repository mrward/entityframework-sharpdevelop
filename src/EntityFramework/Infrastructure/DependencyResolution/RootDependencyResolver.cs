// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

namespace System.Data.Entity.Infrastructure.DependencyResolution
{
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Data.Entity.Core.Metadata.Edm;
    using System.Data.Entity.Core.Objects;
    using System.Data.Entity.Infrastructure;
    using System.Data.Entity.Infrastructure.Interception;
    using System.Data.Entity.Infrastructure.Pluralization;
    using System.Data.Entity.Internal;
    using System.Data.Entity.Migrations.History;
    using System.Data.Entity.ModelConfiguration.Utilities;
    using System.Data.Entity.Utilities;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;

    // <summary>
    // This resolver is always the last resolver in the internal resolver chain and is
    // responsible for providing the default service for each dependency or throwing an
    // exception if there is no reasonable default service.
    // </summary>
    internal class RootDependencyResolver : IDbDependencyResolver
    {
        private readonly ResolverChain _defaultResolvers = new ResolverChain();
        private readonly ResolverChain _resolvers = new ResolverChain();
        private readonly DatabaseInitializerResolver _databaseInitializerResolver;

        public RootDependencyResolver()
            : this(new DefaultProviderServicesResolver(), new DatabaseInitializerResolver())
        {
        }

        [SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
        [SuppressMessage("Microsoft.Reliability", "CA2000: Dispose objects before losing scope")]
        public RootDependencyResolver(
            DefaultProviderServicesResolver defaultProviderServicesResolver,
            DatabaseInitializerResolver databaseInitializerResolver)
        {
            DebugCheck.NotNull(defaultProviderServicesResolver);
            DebugCheck.NotNull(databaseInitializerResolver);

            _databaseInitializerResolver = databaseInitializerResolver;

            _resolvers.Add(new TransactionContextInitializerResolver());
            _resolvers.Add(_databaseInitializerResolver);
            _resolvers.Add(new DefaultExecutionStrategyResolver());
            _resolvers.Add(new CachingDependencyResolver(defaultProviderServicesResolver));
            _resolvers.Add(new CachingDependencyResolver(new DefaultProviderFactoryResolver()));
            _resolvers.Add(new CachingDependencyResolver(new DefaultInvariantNameResolver()));
            _resolvers.Add(new SingletonDependencyResolver<IDbConnectionFactory>(new SqlConnectionFactory()));
            _resolvers.Add(new SingletonDependencyResolver<Func<DbContext, IDbModelCacheKey>>(new DefaultModelCacheKeyFactory().Create));
            _resolvers.Add(new SingletonDependencyResolver<IManifestTokenResolver>(new DefaultManifestTokenResolver()));
            _resolvers.Add(new SingletonDependencyResolver<Func<DbConnection, string, HistoryContext>>(HistoryContext.DefaultFactory));
            _resolvers.Add(new SingletonDependencyResolver<IPluralizationService>(new EnglishPluralizationService()));
            _resolvers.Add(new SingletonDependencyResolver<AttributeProvider>(new AttributeProvider()));
            _resolvers.Add(new SingletonDependencyResolver<Func<DbContext, Action<string>, DatabaseLogFormatter>>((c, w) => new DatabaseLogFormatter(c, w)));
            _resolvers.Add(new SingletonDependencyResolver<Func<DbConnection, TransactionContext>>(c => new TransactionContext(c)));
            _resolvers.Add(new SingletonDependencyResolver<Func<TransactionHandler>>(() => new DefaultTransactionHandler()));

#if NET40
            _resolvers.Add(new SingletonDependencyResolver<IDbProviderFactoryResolver>(new Net40DefaultDbProviderFactoryResolver()));
#else
            _resolvers.Add(new SingletonDependencyResolver<IDbProviderFactoryResolver>(new DefaultDbProviderFactoryResolver()));
#endif
            _resolvers.Add(new SingletonDependencyResolver<IMetadataAnnotationSerializer>(
                new ClrTypeAnnotationSerializer(), XmlConstants.ClrTypeAnnotation));
            _resolvers.Add(new SingletonDependencyResolver<IMetadataAnnotationSerializer>(
                new IndexAnnotationSerializer(), XmlConstants.IndexAnnotation));
        }

        public DatabaseInitializerResolver DatabaseInitializerResolver
        {
            get { return _databaseInitializerResolver; }
        }

        // <inheritdoc />
        public virtual object GetService(Type type, object key)
        {
            return _defaultResolvers.GetService(type, key) ?? _resolvers.GetService(type, key);
        }

        public virtual void AddDefaultResolver(IDbDependencyResolver resolver)
        {
            DebugCheck.NotNull(resolver);

            _defaultResolvers.Add(resolver);
        }

        public IEnumerable<object> GetServices(Type type, object key)
        {
            return _defaultResolvers.GetServices(type, key).Concat(_resolvers.GetServices(type, key));
        }
    }
}
