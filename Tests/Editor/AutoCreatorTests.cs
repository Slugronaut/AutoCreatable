using System.Linq;
using NUnit.Framework;
using Toolbox.AutoCreate;


public class AutoCreatorTests
{
    #region base Mocks
    [AutoCreate]
    public class MockAutoCreationType { }

    [AutoCreate]
    public abstract class MockAbstractAutoCreationType { }

    public class AutoResolvableClass
    {
        [AutoResolve]
        public MockAutoCreationType Mock;
    }

    const int InitializedIntValue = 102;
    [AutoResolve]
    AutoCreatedWithContructor InstWithCtor;

    [AutoCreate]
    public class AutoCreatedWithContructor
    {
        private readonly int _SomeValue;
        public int SomeValue => _SomeValue;

        public AutoCreatedWithContructor()
        {
            _SomeValue = InitializedIntValue;
        }
    }
    #endregion


    #region Interface Mocks
    public interface IAutoResolveMock { }
    [AutoCreate(CreationActions.None, typeof(IAutoResolveMock))]
    public class MockAutoCreationType2 : IAutoResolveMock { }

    public class AutoResolvableInterfaceClass
    {
        [AutoResolve]
        public IAutoResolveMock Mock;
    }
    #endregion


    #region Subclass Mocks
    //[AutoCreate] //uncommenting this will allow us to confirm that an exception is thrown when we binding multiple instances to a single type
    public class MockAutoCreationType3 { }
    [AutoCreate(CreationActions.None, typeof(MockAutoCreationType3))]
    public class MockAutoCreationType4 : MockAutoCreationType3 { }

    public class AutoResolvableDerivedClass
    {
        [AutoResolve]
        public MockAutoCreationType3 MockBase;

        [AutoResolve]
        public MockAutoCreationType4 MockDerived;
    }
    #endregion


    #region Interface vs Concrete Mocks
    public interface IMockInterface { }
    [AutoCreate(CreationActions.None, typeof(IMockInterface))]
    public class MockConcrete : IMockInterface { }

    [AutoResolve]
    IMockInterface MockInst1;

    [AutoResolve]
    MockConcrete MockInst2;
    #endregion



    /// <summary>
    /// 
    /// </summary>
    [Test]
    public void TypesWithAutoCreateAttributeFound()
    {
        var autoCreateableTypes = AutoCreator.FindAllAutoCreatableTypes();
        Assert.IsNotNull(autoCreateableTypes);
        Assert.GreaterOrEqual(autoCreateableTypes.Length, 1);
        Assert.Contains(typeof(MockAutoCreationType), autoCreateableTypes);
    }

    [Test]
    public void AutoCreatablesInstantiated()
    {
        var autoCreatables = AutoCreator.InstantiateAllAutoCreateables().Select(x => x.Value).ToList();
        Assert.IsNotNull(autoCreatables);
        Assert.GreaterOrEqual(autoCreatables.Count, 1);
        Assert.Contains(typeof(MockAutoCreationType), autoCreatables.Select(x => x.GetType()).ToList());
    }

    [Test]
    public void DoesntAutoCreateAbstractTypes()
    {
        Assert.DoesNotThrow(() =>
        {
            var autoCreatables = AutoCreator.InstantiateAllAutoCreateables().ToList();
        });
    }

    /// <summary>
    /// Confirms that the AutoCreator is properly registering the type with the instance created.
    /// </summary>
    [Test]
    public void AutoCreatedTypesAreResolvable()
    {
        AutoCreator.Initialize();

        AutoResolvableClass res = new();
        Assert.IsNull(res.Mock);

        AutoCreator.Resolve(res);
        Assert.IsNotNull(res.Mock);
        AutoCreator.Reset();
    }

    /// <summary>
    /// Confirms that the AutoCreator is properly registering aliased types with the instance created.
    /// </summary>
    [Test]
    public void AutoCreatedTypesAreResolvableByAliasedInterfaces()
    {
        AutoCreator.Initialize();
        AutoResolvableInterfaceClass res = new();
        Assert.IsNull(res.Mock);
        AutoCreator.Resolve(res);

        Assert.IsNotNull(res.Mock);
        Assert.AreEqual(typeof(MockAutoCreationType2), res.Mock.GetType());
        AutoCreator.Reset();
    }

    /// <summary>
    /// Confirms that the AutoCreator is properly registering aliased types with the instance created.
    /// </summary>
    [Test]
    public void AutoCreatedTypesAreResolvableByAliasedSubclasses()
    {
        AutoCreator.Initialize();
        AutoResolvableDerivedClass res = new();
        AutoCreator.Resolve(res);

        Assert.AreEqual(typeof(MockAutoCreationType4), res.MockBase.GetType());
        Assert.AreEqual(typeof(MockAutoCreationType4), res.MockDerived.GetType());
        Assert.AreEqual(res.MockDerived, res.MockBase);
        AutoCreator.Reset();
    }

    [Test]
    public void InvokesDefaultConstructor()
    {
        AutoCreator.Initialize();
        AutoCreator.Resolve(this);

        Assert.NotNull(InstWithCtor);
        Assert.AreEqual(InitializedIntValue, InstWithCtor.SomeValue);

        AutoCreator.Reset();
    }

    [Test]
    public void DifferentResolvedTypesPointToSameObject()
    {
        AutoCreator.Initialize();
        AutoCreator.Resolve(this);

        Assert.NotNull(MockInst1);
        Assert.NotNull(MockInst2);
        Assert.AreEqual(MockInst1, MockInst2);

        AutoCreator.Reset();
    }
}
