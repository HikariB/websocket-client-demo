﻿using System;
using System.Collections.Generic;
using System.Text;
using HFTR.Common.Models;
using HFTR.Common.Models.Socket;
namespace HFTR.Common.Services
{
    public class MonitorDataService
    {
        SocketService socketService;
        log4net.ILog log;

        public class UpdateEventArgs : EventArgs
        {
            public string Instrument { get; }
            public UpdateEventArgs(string instrument)
            {
                Instrument = instrument;
            }
        }

        public event EventHandler<UpdateEventArgs> UpdateValueEvent;
        public event EventHandler<UpdateEventArgs> UpdateOrderEvent;
        public event EventHandler<UpdateEventArgs> UpdatePositionEvent;
        public event EventHandler<UpdateEventArgs> DelayedMarketDataEvent;

        protected virtual void OnUpdateValueEvent(UpdateEventArgs e)
        {
            UpdateValueEvent?.Invoke(this, e);
        }

        protected virtual void OnUpdateOrderEvent(UpdateEventArgs e)
        {
            UpdateOrderEvent?.Invoke(this, e);
        }

        protected virtual void OnUpdatePositionEvent(UpdateEventArgs e)
        {
            UpdatePositionEvent?.Invoke(this, e);
        }

        protected virtual void OnDelayedMarketDataEvent(UpdateEventArgs e)
        {
            DelayedMarketDataEvent?.Invoke(this, e);
        }

        public MonitorData Data { get; private set; } = new MonitorData();
        public LoginInfo LoginInfo { get; private set; }
        public bool LoginResult { get; private set; }
        public List<Topic> Topics { get; private set; }
        public Dictionary<Topic, bool> SubResults { get; private set; } = new Dictionary<Topic, bool>();

        public MonitorDataService(SocketService socketService, LoginInfo loginJson, List<Topic> topics, log4net.ILog log)
        {
            this.socketService = socketService;
            socketService.OpenedEvent += OpenedHandler;
            socketService.LoginResultEvent += LoginResultHandler;
            socketService.SubResultEvent += SubResulHandler;
            socketService.InstrumentInfoEvent += InstrumentInfoHandler;
            socketService.InitPositionEvent += InitPositionHandler;
            socketService.OrderRtnEvent += OrderRtnHandler;
            socketService.TradeRtnEvent += TradeRtnHandler;
            socketService.DepthMarketDataEvent += DepthMarketDataHandler;

            this.LoginInfo = loginJson;
            this.Topics = topics;
            this.log = log;
        }

        void OpenedHandler(object sender, EventArgs e)
        {
            log.Info("Opened");

            socketService.Login(LoginInfo);
        }

        void LoginResultHandler(object sender, SocketService.MessageEventArgs<LoginResult> e)
        {
            var result = e.Message;

            if (result.Result == "success")
            {
                log.Info("LoginResult\t" + result.Account + "\t" + result.Result);
                LoginResult = true;
                socketService.Subscribe(Topics.ToArray());
            }
            else
            {
                log.Info("LoginResult\t" + result.Account + "\t" + result.Result + "\t" + ((LoginFail)result).Reason);
            }
        }

        void SubResulHandler(object sender, SocketService.MessageEventArgs<SubResult> e)
        {
            var result = e.Message;
            if (SubResults.ContainsKey(result.Topic))
            {
                SubResults[result.Topic] = result.Result == "success";
            }
            else
            {
                SubResults.Add(result.Topic, result.Result == "success");
            }

            if (result.Result == "success")
            {
                log.Info("SubResult\t" + result.Topic + "\t" + result.Result);
            }
            else
            {
                log.Info("SubResult\t" + result.Topic + "\t" + result.Result + "\t" + ((SubFail)result).Reason);
            }

        }

        void InstrumentInfoHandler(object sender, SocketService.MessageEventArgs<Publish<InstrumentInfo>> e)
        {
            var loginInfo = e.Message.Data;
            Data.Instruments.Add(loginInfo.InstrumentId, new InstrumentData());
            var instrument = Data.Instruments[loginInfo.InstrumentId];
            instrument.InstrumentId = loginInfo.InstrumentId;
            instrument.ContractMultiplier = loginInfo.ContractMultiplier;
            instrument.PreSettlementPrice = loginInfo.PreSettlementPrice;
            instrument.CurrentPrice = loginInfo.PreSettlementPrice;

            log.Info("InstrumentInfo\t" + loginInfo.InstrumentId + "\tContractMultipler\t" + instrument.ContractMultiplier
                + "\tPreSettlementPrice\t" + loginInfo.PreSettlementPrice);
        }

        void InitPositionHandler(object sender, SocketService.MessageEventArgs<Publish<InitPosition>> e)
        {
            var position = e.Message.Data;
            var instrument = Data.Instruments[position.InstrumentId];
            instrument.InitLongPosition = position.LongPos;
            instrument.CurrentLongPosition = position.LongPos;
            instrument.InitShortPosition = position.ShortPos;
            instrument.CurrentShortPosition = position.ShortPos;
            instrument.PositionCost = instrument.PreSettlementPrice * (position.LongPos - position.ShortPos);

            log.Info("InitPosition\t" + position.InstrumentId + "\tLong\t" + position.LongPos + "\tShort\t" + position.ShortPos);

            OnUpdatePositionEvent(new UpdateEventArgs(position.InstrumentId));
        }

        void OrderRtnHandler(object sender, SocketService.MessageEventArgs<Publish<OrderRtn>> e)
        {
            var rtn = e.Message.Data;
            var order = Data.Orders;
            var instrument = Data.Instruments[rtn.InstrumentId];

            // Update IsMarketDataDelayed
            if (instrument.IsMarketDataValid == true)
            {
                var time1 = DateTime.Now;
                var time2 = Convert.ToDateTime(instrument.UpdateTime);
                var ts1 = new TimeSpan(time1.Ticks);
                var ts2 = new TimeSpan(time2.Ticks);
                var ts = ts1.Subtract(ts2).Duration();
                if (ts.Seconds > 10)
                {
                    instrument.IsMarketDataValid = false;
                    OnDelayedMarketDataEvent(new UpdateEventArgs(instrument.InstrumentId));
                }
            }

            if (order.ContainsKey(rtn.OrderSysId))
            {
                if (!rtn.IsFinished())
                    order[rtn.OrderSysId].TradedVolume = rtn.TradedVolume;
                else
                {
                    order.Remove(rtn.OrderSysId);
                    if (rtn.OrderStatus == OrderStatus.Canceled)
                        instrument.OrderCancelNum += 1;
                }
            }
            else
            {
                instrument.OrderInsertNum += 1;
                instrument.OrderFee += 1;
                instrument.OrderVolume += rtn.TotalVolume;
                if (!rtn.IsFinished())
                    order.Add(rtn.OrderSysId, new OrderData(rtn));
            }

            log.Info("OrderRtn\t" + rtn.OrderSysId + "\t" + rtn.InstrumentId + "\t" + rtn.Direction + "\t" + rtn.OffsetFlag + "\t" + rtn.Price + "\t" + rtn.TotalVolume + "\t" + rtn.TradedVolume + "\t" + rtn.OrderStatus);
            log.Info("UpdateOrder\t" + rtn.InstrumentId + "\tOrderInsertNum\t" + instrument.OrderInsertNum + "\tOrderCancelNum\t" + instrument.OrderCancelNum
                + "\tOrderFee\t" + instrument.OrderFee + "\tTotalOrderFee\t" + Data.TotalOrderFee);

            OnUpdateOrderEvent(new UpdateEventArgs(rtn.InstrumentId));
        }

        void TradeRtnHandler(object sender, SocketService.MessageEventArgs<Publish<TradeRtn>> e)
        {
            var rtn = e.Message.Data;
            var instrument = Data.Instruments[rtn.InstrumentId];

            // Update IsMarketDataDelayed
            if (instrument.IsMarketDataValid == true)
            {
                var time1 = DateTime.Now;
                var time2 = Convert.ToDateTime(instrument.UpdateTime);
                var ts1 = new TimeSpan(time1.Ticks);
                var ts2 = new TimeSpan(time2.Ticks);
                var ts = ts1.Subtract(ts2).Duration();
                if (ts.Seconds > 10)
                {
                    instrument.IsMarketDataValid = false;
                    OnDelayedMarketDataEvent(new UpdateEventArgs(instrument.InstrumentId));
                }
            }

            instrument.Fee += instrument.ContractMultiplier * rtn.Price * rtn.Volume * rtn.FeeRate;
            instrument.TradeVolume += rtn.Volume;

            if (rtn.Direction == Direction.Buy)
                instrument.PositionCost += rtn.Price * rtn.Volume;
            else
                instrument.PositionCost -= rtn.Price * rtn.Volume;

            if (rtn.Direction == Direction.Buy && rtn.OffsetFlag == OffsetFlag.Open)
                instrument.CurrentLongPosition += rtn.Volume;
            else if (rtn.Direction == Direction.Sell && rtn.OffsetFlag == OffsetFlag.Close)
                instrument.CurrentLongPosition -= rtn.Volume;
            else if (rtn.Direction == Direction.Sell && rtn.OffsetFlag == OffsetFlag.Open)
                instrument.CurrentShortPosition += rtn.Volume;
            else if (rtn.Direction == Direction.Buy && rtn.OffsetFlag == OffsetFlag.Close)
                instrument.CurrentShortPosition -= rtn.Volume;

            log.Info("TradeRtn\t" + rtn.OrderSysId + "\t" + rtn.InstrumentId + "\t" + rtn.Direction + "\t" + rtn.OffsetFlag + "\t" + rtn.Price + "\t" + rtn.Volume);

            log.Info("UpdateTrade\t" + rtn.InstrumentId + "\tTradeVolume\t" + instrument.TradeVolume + "\tTotalTradeVolume\t" + instrument.Volume);

            log.Info("UpdatePosition\t" + rtn.InstrumentId + "\t" + instrument.NetPosition + "\t" + instrument.CurrentLongPosition + "\t" + instrument.CurrentShortPosition);

            log.Info("UpdateFee\t" + rtn.InstrumentId + "\tFee\t" + instrument.Fee + "\tTotalFee\t" + Data.TotalFee);

            OnUpdatePositionEvent(new UpdateEventArgs(rtn.InstrumentId));

            if (instrument.IsMarketDataValid == true)
            {
                log.Info("UpdateValue\t" + rtn.InstrumentId + "\tCurrentValue\t" + instrument.CurrentValue + "\tPositionCost\t" + instrument.PositionCost + "\tProfit\t" + instrument.Profit + "\tTotalProfit\t" + Data.TotalProfit);
                OnUpdateValueEvent(new UpdateEventArgs(rtn.InstrumentId));
            }

        }

        void DepthMarketDataHandler(object sender, SocketService.MessageEventArgs<Publish<DepthMarketData>> e)
        {
            var md = e.Message.Data;
            var instrument = Data.Instruments[md.InstrumentId];

            var UpdateNotValid = false;
            // Update IsMarketDataDelayed
            if (instrument.IsMarketDataValid == true)
            {
                var time1 = DateTime.Now;
                var time2 = Convert.ToDateTime(instrument.UpdateTime);
                var ts1 = new TimeSpan(time1.Ticks);
                var ts2 = new TimeSpan(time2.Ticks);
                var ts = ts1.Subtract(ts2).Duration();
                if (ts.Seconds > 10)
                {
                    instrument.IsMarketDataValid = false;
                    UpdateNotValid = true;
                }
            }

            // filter invalid md
            // update time invalid
            // price/volume invalid
            var time3 = DateTime.Now;
            var time4 = Convert.ToDateTime(md.UpdateTime);
            var ts3 = new TimeSpan(time3.Ticks);
            var ts4 = new TimeSpan(time4.Ticks);
            var tsFromMd = ts3.Subtract(ts4).Duration();
            if (tsFromMd.Seconds > 10 || md.Bids[0][1] == 0 || md.Asks[0][1] == 0)
            {
                instrument.IsMarketDataValid |= false;
            }
            else
            {
                instrument.IsMarketDataValid = true;
                instrument.UpdateTime = md.UpdateTime;
                instrument.CurrentPrice = 0.5 * (md.Bids[0][0] + md.Asks[0][0]);
                instrument.Volume = md.Volume;

                log.Info("DepthMarketData\t" + md.InstrumentId + "\t" + instrument.CurrentPrice + "\t" + md.UpdateTime + "\t" + md.UpdateMillisec);

                log.Info("UpdateValue\t" + md.InstrumentId + "\tCurrentValue\t" + instrument.CurrentValue + "\tPositionCost\t" + instrument.PositionCost + "\tProfit\t" + instrument.Profit + "\tTotalProfit\t" + Data.TotalProfit);
                OnUpdateValueEvent(new UpdateEventArgs(md.InstrumentId));
            }

            if (UpdateNotValid == true && instrument.IsMarketDataValid == false)
                OnDelayedMarketDataEvent(new UpdateEventArgs(instrument.InstrumentId));
        }
    }
}
