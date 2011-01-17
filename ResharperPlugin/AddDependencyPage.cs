using System;
using System.Collections.Generic;
using JetBrains.Application;
using JetBrains.Application.Progress;
using JetBrains.CommonControls.Validation;
using JetBrains.DataFlow;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.CSharp.Util;
using JetBrains.ReSharper.Features.Common.GoToByName;
using JetBrains.ReSharper.Features.Common.UI;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Refactorings.Workflow;
using JetBrains.UI.CommonControls;
using JetBrains.UI.Interop;

namespace ResharperPlugin
{
    public class AddDependencyPage: SafeUserControl, IValidatorProvider, IRefactoringPage
    {
        private readonly ISolution _solution;
        private readonly IProperty<Boolean> _continueEnabled;

        private readonly CompletionPickerEdit _typeEditBox;
        private readonly CSharpTypeValidator _typeValidator;

        public AddDependencyPage(Action<string> updateParameterType, ISolution solution)
        {
            _solution = solution;
            _continueEnabled = new Property<bool>("ContinueEnabled", true);

            _typeEditBox = new CompletionPickerEdit {Width = 300, Solution = solution};
            _typeEditBox.Settings.Value = TypeChooser.CreateSettings(
                solution,
                LibrariesFlag.SolutionAndLibraries,
                CSharpLanguageService.CSHARP);
            _typeEditBox.Text.Change.Advise_HasNew(
                args =>
                {
                    updateParameterType(args.New);
                    UpdateUI();
                });

            Controls.Add(_typeEditBox);
            _typeValidator = new CSharpTypeValidator();
        }

        private void UpdateUI()
        {
            Shell.Instance.Invocator.ReentrancyGuard.ExecuteOrQueue("AddDependencyPage.UpdateUI", delegate
            {
                if (IsDisposed) 
                    return;
                using (ReadLockCookie.Create())
                    PsiManager.GetInstance(_solution).CommitAllDocuments();
                
                FormValidator.GetInstance(this).Update();
            });

        }

        public IEnumerable<IValidator> Validators
        {
            get
            {
                return new []
                       {
                           new TextValidatorReentrantSafe(
                               _typeEditBox, 
                               ValidatorSeverity.Error, 
                               "Dependency type is not valid", 
                               returnTypeText => _typeValidator.IsValidReturnType(returnTypeText))
                       };
            }
        }

        public IRefactoringPage Commit(IProgressIndicator pi)
        {
            return null;
        }

        public bool Initialize(IProgressIndicator pi)
        {
            pi.Start(1);
            pi.Stop();
            return true;
        }

        public bool RefreshContents(IProgressIndicator pi)
        {
            pi.Start(1);
            pi.Stop();
            return true;
        }

        public IProperty<bool> ContinueEnabled
        {
            get { return _continueEnabled; }
        }

        public string Description
        {
            get { return "Provide the type of the dependency you would like to add."; }
        }

        public string Title
        {
            get { return "Add dependency"; }
        }

        public EitherControl View
        {
            get { return this; }
        }

        public bool DoNotShow
        {
            get { return false; }
        }
    }
}