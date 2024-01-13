using System.Linq;
using NUnit.Framework;
using Peg.AutoCreate;


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
        AutoCreator.Initialize();
        var autoCreateableTypes = AutoCreator.FindAllAutoCreatableTypes();
        Assert.IsNotNull(autoCreateableTypes);
        Assert.GreaterOrEqual(autoCreateableTypes.Length, 1);
        Assert.Contains(typeof(MockAutoCreationType), autoCreateableTypes);
        AutoCreator.Reset();
    }

    [Test]
    public void AutoCreatablesInstantiated()
    {
        AutoCreator.Initialize();
        var autoCreatables = AutoCreator.InstantiateAllAutoCreateables().Select(x => x.Value).ToList();
        Assert.IsNotNull(autoCreatables);
        Assert.GreaterOrEqual(autoCreatables.Count, 1);
        Assert.Contains(typeof(MockAutoCreationType), autoCreatables.Select(x => x.GetType()).ToList());
        AutoCreator.Reset();
    }

    [Test]
    public void DoesntAutoCreateAbstractTypes()
    {
        AutoCreator.Initialize();
        Assert.DoesNotThrow(() =>
        {
            var autoCreatables = AutoCreator.InstantiateAllAutoCreateables().ToList();
        });
        AutoCreator.Reset();
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


    #region Testing AutoAwake and AutoStart
    [AutoCreate]
    public class MockAutoAwake
    {
        bool AwakeFlag;
        public bool FlagWasSetInAwakeFirst;
        public int AwakeCalls = 0;
        public int StartCalls = 0;

        void AutoAwake()
        {
            AwakeFlag = true;
            AwakeCalls++;
        }

        void AutoStart()
        {
            FlagWasSetInAwakeFirst = AwakeFlag;
            StartCalls++;
        }
    }


    public interface IMockMultiAwake { }
    [AutoCreate(CreationActions.None, typeof(IMockMultiAwake))]
    public class MockMultiAwake : IMockMultiAwake
    {
        bool AwakeFlag;
        public bool FlagWasSetInAwakeFirst;
        public int AwakeCalls = 0;
        public int StartCalls = 0;

        void AutoAwake()
        {
            AwakeFlag = true;
            AwakeCalls++;
        }

        void AutoStart()
        {
            FlagWasSetInAwakeFirst = AwakeFlag;
            StartCalls++;
        }
    }


    [AutoCreate]
    public class MockAutoDestroy
    {
        public AutoCreatorTests InjectSource;

        void AutoDestroy()
        {
            if(InjectSource != null)
                InjectSource.DestroyCount++;
        }
    }

    public interface IMockMultiDestroy { }
    [AutoCreate(CreationActions.None, typeof(IMockMultiDestroy))]
    public class MockMultiDestroy : IMockMultiDestroy
    {
        public AutoCreatorTests InjectSource;

        void AutoDestroy()
        {
            if(InjectSource != null)
                InjectSource.DestroyCount++;
        }
    }


    [AutoResolve]
    MockAutoAwake Awakable;

    [AutoResolve]
    MockMultiAwake MultiAwakable;

    [AutoResolve]
    MockAutoDestroy Destroyable;

    [AutoResolve]
    MockMultiDestroy MultiDestroyable;

    int DestroyCount;



    [Test]
    public void InvokesAutoAwake()
    {
        AutoCreator.Initialize();
        AutoCreator.Resolve(this);

        Assert.AreEqual(1, Awakable.AwakeCalls);

        AutoCreator.Reset();
    }

    [Test]
    public void InvokesAutoStart()
    {
        AutoCreator.Initialize();
        AutoCreator.Resolve(this);

        Assert.AreEqual(1, Awakable.StartCalls);

        AutoCreator.Reset();

    }

    [Test]
    public void InvokesAwakeBeforeStart()
    {
        AutoCreator.Initialize();
        AutoCreator.Resolve(this);

        Assert.IsTrue(Awakable.FlagWasSetInAwakeFirst);

        AutoCreator.Reset();
    }

    [Test]
    public void AliasTypesDontInvokeAutoAwakeOrStartTwice()
    {
        AutoCreator.Initialize();
        AutoCreator.Resolve(this);

        Assert.AreEqual(1, MultiAwakable.AwakeCalls);
        Assert.AreEqual(1, MultiAwakable.StartCalls);

        AutoCreator.Reset();
    }

    [Test]
    public void InvokesAutoDestroy()
    {
        DestroyCount = 0;
        AutoCreator.Initialize();
        AutoCreator.Resolve(this);

        Assert.IsNotNull(Destroyable);
        Assert.AreEqual(0, DestroyCount);
        Destroyable.InjectSource = this;

        AutoCreator.Reset();

        Assert.AreEqual(1, DestroyCount);
    }

    [Test]
    public void AliasTypesDontInvokeAutoDestroyTwice()
    {
        DestroyCount = 0;
        AutoCreator.Initialize();
        AutoCreator.Resolve(this);

        Assert.IsNotNull(MultiDestroyable);
        Assert.AreEqual(0, DestroyCount);
        MultiDestroyable.InjectSource = this;

        AutoCreator.Reset();

        Assert.AreEqual(1, DestroyCount);
    }
    #endregion
}
