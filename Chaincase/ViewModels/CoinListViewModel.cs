﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Logging;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionProcessing;
using Chaincase.Navigation;
using Splat;

namespace Chaincase.ViewModels
{
	public class CoinListViewModel : ViewModelBase
	{
		private CompositeDisposable Disposables { get; set; }

		public SourceList<CoinViewModel> RootList { get; private set; }

        private ReadOnlyObservableCollection<CoinViewModel> _coinViewModels;

        private string _selectedAmountText;
        private Money _selectedAmount;
        private bool _isCoinListLoading;
        private bool _isAnyCoinSelected;
        private object SelectionChangedLock { get; } = new object();
        private object StateChangedLock { get; } = new object();

        public event EventHandler CoinListShown;
        public event EventHandler<CoinViewModel> SelectionChanged;

        public ReadOnlyObservableCollection<CoinViewModel> Coins => _coinViewModels;

		public CoinListViewModel()
            : base(Locator.Current.GetService<IViewStackService>())
		{
            RootList = new SourceList<CoinViewModel>();
            RootList
                .Connect()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Bind(out _coinViewModels)
                .Subscribe();

            Disposables = Disposables is null ?
               new CompositeDisposable() :
               throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");

            Observable
                .Merge(Observable.FromEventPattern<ProcessedResult>(Global.WalletService.TransactionProcessor, nameof(Global.WalletService.TransactionProcessor.WalletRelevantTransactionProcessed)).Select(_ => Unit.Default))
                .Throttle(TimeSpan.FromSeconds(1)) // Throttle TransactionProcessor events adds/removes.
                .Merge(Observable.FromEventPattern(this, nameof(CoinListShown), RxApp.MainThreadScheduler).Select(_ => Unit.Default)) // Load the list immediately.
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(args =>
                {
                    try
                    {
                        var actual = Global.WalletService.TransactionProcessor.Coins.ToHashSet();
                        var old = RootList.Items.ToDictionary(c => c.Model, c => c);

                        var coinToRemove = old.Where(c => !actual.Contains(c.Key)).ToArray();
                        var coinToAdd = actual.Where(c => !old.ContainsKey(c)).ToArray();

                        RootList.RemoveMany(coinToRemove.Select(kp => kp.Value));

                        var newCoinViewModels = coinToAdd.Select(c => new CoinViewModel(this, c)).ToArray();
                        RootList.AddRange(newCoinViewModels);

                        var allCoins = RootList.Items.ToArray();

                        foreach (var item in coinToRemove)
                        {
                            item.Value.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex);
                    }
                    finally
                    {
                        IsCoinListLoading = false;
                    }
                })
                .DisposeWith(Disposables);
        }

		private void ClearRootList() => RootList.Clear();

		public void AfterDismissed()
		{
			ClearRootList();

			Disposables?.Dispose();
		}

        public void OnCoinIsSelectedChanged(CoinViewModel cvm)
        {
            SelectionChanged?.Invoke(this, cvm);
            SelectedAmount = Coins.Where(x => x.IsSelected).Sum(x => x.Amount);
            SelectedAmountText = SelectedAmount.ToString();
        }

        public string SelectedAmountText
        {
            get => _selectedAmountText;
            set => this.RaiseAndSetIfChanged(ref _selectedAmountText, $"{value} BTC Selected");
        }

        public Money SelectedAmount
        {
            get => _selectedAmount;
            set => this.RaiseAndSetIfChanged(ref _selectedAmount, value);
        }

        public bool IsCoinListLoading
        {
            get => _isCoinListLoading;
            set => this.RaiseAndSetIfChanged(ref _isCoinListLoading, value);
        }

        public bool IsAnyCoinSelected
        {
            get => _isAnyCoinSelected;
            set => this.RaiseAndSetIfChanged(ref _isAnyCoinSelected, value);
        }

    }
}
