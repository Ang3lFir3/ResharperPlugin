using System;
using System.Collections.Generic;
using System.Text;
using JetBrains.Application.Progress;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Bulbs;
using JetBrains.ReSharper.Intentions;
using JetBrains.ReSharper.Intentions.CSharp.DataProviders;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.TextControl;
using JetBrains.Util;

namespace ResharperPlugin
{
    public abstract class ChangeCallToRhinoCallContextAction: BulbItemImpl, IContextAction
    {
        private readonly ICSharpContextActionDataProvider _provider;

        protected ChangeCallToRhinoCallContextAction(ICSharpContextActionDataProvider provider)
        {
            _provider = provider;
        }

        protected override Action<ITextControl> ExecuteTransaction(ISolution solution, IProgressIndicator progress)
        {
            var invocation = _provider.GetSelectedElement<IInvocationExpression>(false, false);
            if (invocation == null || invocation.Reference == null)
                return null;

            var referenceExpression = invocation.InvokedExpression as IReferenceExpression;
            if (referenceExpression == null)
                return null;

            var invocationExpression = invocation as IInvocationExpressionNode;
            if (invocationExpression == null)
                return null;
            var textRange = new TextRange(referenceExpression.Reference.GetTreeTextRange().StartOffset.Offset,
                                          invocationExpression.RPar.GetTreeStartOffset().Offset + 1);
            var argumentsText = _provider.Document.GetText(textRange);

            _provider.Document.ReplaceText(textRange, MethodName + "(x => x." + argumentsText + ")");
            return null;
        }

        protected abstract string MethodName { get; }

        public override string Text
        {
            get { return "Change this call to " + MethodName + "(x => <call>)"; }
        }

        public bool IsAvailable(IUserDataHolder cache)
        {
            var invocation = _provider.GetSelectedElement<IInvocationExpression>(false, false);
            if (invocation == null || invocation.Reference == null)
                return false;

            var referenceExpression = invocation.InvokedExpression as IReferenceExpression;
            if (referenceExpression == null)
                return false;

            switch(referenceExpression.Reference.GetName())
            {
                case "Stub":
                case "AssertWasCalled":
                case "AssertWasNotCalled":
                    return false;
            }

            var qualifierExpression = referenceExpression.QualifierExpression as IReferenceExpression;
            if (qualifierExpression == null)
                return false;

            var qualifierType = qualifierExpression.Type();
            if (!qualifierType.GetPresentableName(CSharpLanguageService.CSHARP).StartsWith("I"))
                return false;

            return true;
        }
    }

    [ContextAction(Description = "Change this call to Stub(x => <call>)", Name = "ChangeCallToStub", Priority = 0x7FFE)]
    public class ChangeCallToStubContextAction: ChangeCallToRhinoCallContextAction
    {
        public ChangeCallToStubContextAction(ICSharpContextActionDataProvider provider) : base(provider)
        {
        }

        protected override string MethodName
        {
            get { return "Stub"; }
        }
    }

    [ContextAction(Description = "Change this call to AssertWasCalled(x => <call>)", Name = "ChangeCallToAssertWasCalled", Priority = 0x7FFD)]
    public class ChangeCallToAssertWasCalledContextAction : ChangeCallToRhinoCallContextAction
    {
        public ChangeCallToAssertWasCalledContextAction(ICSharpContextActionDataProvider provider)
            : base(provider)
        {
        }

        protected override string MethodName
        {
            get { return "AssertWasCalled"; }
        }
    }

    [ContextAction(Description = "Change this call to AssertWasNotCalled(x => <call>)", Name = "ChangeCallToAssertWasNotCalled", Priority = 0x7FFC)]
    public class ChangeCallToAssertWasNotCalledContextAction : ChangeCallToRhinoCallContextAction
    {
        public ChangeCallToAssertWasNotCalledContextAction(ICSharpContextActionDataProvider provider)
            : base(provider)
        {
        }

        protected override string MethodName
        {
            get { return "AssertWasNotCalled"; }
        }
    }

}
