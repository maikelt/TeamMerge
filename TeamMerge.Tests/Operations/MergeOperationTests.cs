﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rhino.Mocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeamMerge.Exceptions;
using TeamMerge.Helpers;
using TeamMerge.Instellingen.Enums;
using TeamMerge.Operations;
using TeamMerge.Services;
using TeamMerge.Services.Models;
using TeamMerge.Utils;

namespace TeamMerge.Tests.Operations
{
    [TestClass]
    public class MergeOperationTests
    {
        private const string DO_GET_LATEST_ON_BRANCH_METHOD_NAME = "DoGetLatestOnBranchAsync";
        private const string CHECK_WORKSPACE_METHOD_NAME = "CheckIfWorkspaceHasIncludedPendingChangesAsync";

        private MergeOperation _sut;

        private IMergeService _mergeService;
        private IConfigHelper _configHelper;

        private WorkspaceModel _currentWorkspaceModel;
        private string _sourceBranchName;
        private string _targetbranchName;
        
        [TestInitialize]
        public void Initialize()
        {
            _mergeService = MockRepository.GenerateStrictMock<IMergeService>();
            _configHelper = MockRepository.GenerateStrictMock<IConfigHelper>();

            _sut = new MergeOperation(_mergeService, _configHelper);

            _currentWorkspaceModel = new WorkspaceModel
            {
                OwnerName = "MyOwnerName",
                Name = "WorkspaceName"
            };

            _sourceBranchName = "SourceBranchName";
            _targetbranchName = "TargetBranchName";
        }

        [TestMethod]
        [ExpectedException(typeof(MergeActionException))]
        public async Task MergeOperation_CheckIfWorkspaceHasIncludedPendingChangesAsync_WhenCalledWithPendingChangesInWorkspace_ThenThrowsException()
        {
            _configHelper.Expect(x => x.GetValue<bool>(ConfigKeys.ENABLE_WARNING_WHEN_PENDING_CHANGES))
                .Return(true);

            _mergeService.Expect(x => x.HasIncludedPendingChanges(_currentWorkspaceModel))
                .Return(true);

            _sut.MyCurrentAction += InvokesMyCurrentActionWithCheckingPendingChanges;
            
            var obj = new PrivateObject(_sut);
            await (Task) obj.Invoke(CHECK_WORKSPACE_METHOD_NAME, _currentWorkspaceModel);
        }

        private void InvokesMyCurrentActionWithCheckingPendingChanges(object sender, string e)
        {
            Assert.AreEqual(Resources.CheckingPendingChanges, e);
        }

        [TestMethod]
        [ExpectedException(typeof(MergeActionException))]
        public async Task MergeOperation_DoGetLatestOnBranchAsync_WhenCalledAndShouldNotResolveConflicts_ConflictsOccur_ThenThrowsException()
        {
            ChecksIfMyCurrentActionIsCorrectlySet(Branch.Source);

            _configHelper.Expect(x => x.GetValue<int>(ConfigKeys.LATEST_VERSION_FOR_BRANCH))
                .Return((int) Branch.Source);

            _mergeService.Expect(x => x.GetLatestVersion(_currentWorkspaceModel, _sourceBranchName))
                .Return(Task.FromResult(true));

            _configHelper.Expect(x => x.GetValue<bool>(ConfigKeys.SHOULD_RESOLVE_CONFLICTS))
                .Return(false);

            var obj = new PrivateObject(_sut);
            await (Task)obj.Invoke(DO_GET_LATEST_ON_BRANCH_METHOD_NAME, _currentWorkspaceModel, _sourceBranchName, _targetbranchName);
        }

        [TestMethod]
        [ExpectedException(typeof(MergeActionException))]
        public async Task MergeOperation_DoGetLatestOnBranchAsync_WhenCalledAndShouldResolveConflicts_ButUserCancelsTheResolve_ThenThrowsException()
        {
            ChecksIfMyCurrentActionIsCorrectlySet(Branch.Target);

            _configHelper.Expect(x => x.GetValue<int>(ConfigKeys.LATEST_VERSION_FOR_BRANCH))
                .Return((int)Branch.Target);

            _mergeService.Expect(x => x.GetLatestVersion(_currentWorkspaceModel, _targetbranchName))
                .Return(Task.FromResult(true));

            _configHelper.Expect(x => x.GetValue<bool>(ConfigKeys.SHOULD_RESOLVE_CONFLICTS))
                .Return(true);

            _mergeService.Expect(x => x.ResolveConflicts(_currentWorkspaceModel))
                .Return(Task.FromResult(0));

            _mergeService.Expect(x => x.HasConflicts(_currentWorkspaceModel))
                .Return(true);

            var obj = new PrivateObject(_sut);
            await (Task)obj.Invoke(DO_GET_LATEST_ON_BRANCH_METHOD_NAME, _currentWorkspaceModel, _sourceBranchName, _targetbranchName);
        }

        [TestMethod]
        public async Task MergeOperation_DoGetLatestOnBranchAsync_WhenCalledAndShouldResolveConflicts_AndEverythingGoesRight_ThenNothingHappens()
        {
            ChecksIfMyCurrentActionIsCorrectlySet(Branch.SourceAndTarget);

            _configHelper.Expect(x => x.GetValue<int>(ConfigKeys.LATEST_VERSION_FOR_BRANCH))
                .Return((int)Branch.SourceAndTarget);

            _mergeService.Expect(x => x.GetLatestVersion(_currentWorkspaceModel, _targetbranchName, _sourceBranchName))
                .Return(Task.FromResult(true));

            _configHelper.Expect(x => x.GetValue<bool>(ConfigKeys.SHOULD_RESOLVE_CONFLICTS))
                .Return(true);

            _mergeService.Expect(x => x.ResolveConflicts(_currentWorkspaceModel))
                .Return(Task.CompletedTask);

            _mergeService.Expect(x => x.HasConflicts(_currentWorkspaceModel))
                .Return(false);

            var obj = new PrivateObject(_sut);
            await (Task)obj.Invoke(DO_GET_LATEST_ON_BRANCH_METHOD_NAME, _currentWorkspaceModel, _sourceBranchName, _targetbranchName);
        }

        private void ChecksIfMyCurrentActionIsCorrectlySet(Branch branch)
        {
            _sut.MyCurrentAction += (s, action) =>
            {
                Assert.IsTrue(action.Contains(branch.GetDescription()));
            };
        }

        [TestMethod]
        public async Task MergeOperation_Execute_WhenCalled_NothingGoesWrong()
        {
            var orderedChangesetIds = new List<int> { 2, 5, 7, 8 };

            _configHelper.Expect(x => x.GetValue<bool>(ConfigKeys.ENABLE_WARNING_WHEN_PENDING_CHANGES))
                .Return(false);

            _configHelper.Expect(x => x.GetValue<int>(ConfigKeys.LATEST_VERSION_FOR_BRANCH))
                .Return((int)Branch.None);

            _sut.MyCurrentAction += (s, action) =>
            {
                Assert.AreEqual(Resources.MergingBranches, action);
            };

            _mergeService.Expect(x => x.MergeBranches(_currentWorkspaceModel, _sourceBranchName, _targetbranchName, 2, 8)).Return(Task.CompletedTask);
            _mergeService.Expect(x => x.AddWorkItemsAndNavigate(_currentWorkspaceModel, orderedChangesetIds)).Return(Task.CompletedTask);

            await _sut.Execute(new MergeModel
            {
                OrderedChangesetIds = new List<int> { 2, 5, 7, 8 },
                SourceBranch = _sourceBranchName,
                TargetBranch = _targetbranchName,
                WorkspaceModel = _currentWorkspaceModel
            });
        }        

        [TestCleanup]
        public void CleanUp()
        {
            _mergeService.VerifyAllExpectations();
            _configHelper.VerifyAllExpectations();
        }
    }
}
