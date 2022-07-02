from client import Client
import time
from typing import List, Callable

class TransaqConnector:

    class OrderType:
        PutInQueue = 'PutInQueue'
        FOK = 'FOK'
        IOC = 'IOC'

    class BuySell:
        buy = 'B'
        sell = 'S'

    class CondType:
        Bid = 'Bid'
        BidOrLast = 'BidOrLast'
        Ask = 'Ask'
        AskOrLast = 'AskOrLast'
        Time = 'Time'
        CovDown = 'CovDown'
        CovUp = 'CovUp'
        LastUp = 'LastUp'
        LastDown = 'LastDown'

    def __init__(self, ports : List[int] = None, news_callback : Callable = None, accoiunt_callback : Callable = None):
        self.ADDRESS = '127.0.0.1'
        if ports is not None:
            self.PORTs = ports
        else:
            self.PORTs = [11000, 11001, 11002, 11003, 11004, 11005]

        self.news_callback = news_callback
        self.account_callback = accoiunt_callback
        self.client = Client(self.ADDRESS, self.PORTs, self.news_callback)

        self.client.connect2server()

    def connection_required(f):
        def _connection_required(self, *args, **kwargs):
            if self.client._is_connected:
                return f(self, *args, **kwargs)
            else:
                print('Connect to use this method.')

        return _connection_required

    def connect(self, login: str) -> bool:

        return self.client.connect2transaq(login)

    def close(self):
        self.client.close_connection()

    @connection_required
    def get_old_news(self, count : int = 10, callback : Callable = print) -> None:
        self.client.get_old_news(count, callback)

    @connection_required
    def get_news_by_id(self, news_id : str, callback : Callable = print) -> None:
        self.client.get_news_by_id(news_id=news_id, callback=callback)

    @connection_required
    def subscribe(self, method : str, seccodes : List[str], boards : List[str], callback : Callable = None) -> None:

        if len(boards) != len(seccodes):
            print('The number of seccodes shall equals the number of boards')
            return
        data = ','.join(seccodes) + ';' + ','.join(boards) + '\0'
        pairs = [(s,b) for s,b in zip(seccodes, boards)]
        self.client.subscribe(method, data, pairs, callback=callback)

    @connection_required
    def subscribe_ticks(self, seccode : str, board : str, tradeno : int, filter : bool = True, callback : Callable = None) -> None:

        filter = 'true' if filter else 'false'
        data = ','.join([seccode, board, str(tradeno), filter]) + '\0'
        pair = (seccode, board)
        self.client.subscribe_ticks(data, pair, callback=callback)

    @connection_required
    def unsubscribe(self, method : str, seccodes : List[str], boards : List[str]) -> None:
        if len(boards) != len(seccodes):
            print('The number of seccodes shall equals the number of boards')
            return
        data = ','.join(seccodes) + ';' + ','.join(boards) + '\0'
        pairs = [(s,b) for s,b in zip(seccodes, boards)]
        self.client.unsubscribe(method, data, pairs)

    @connection_required
    def unsubscribe_ticks(self) -> None:
        self.client.unsubscribe_ticks()

    @connection_required
    def disconnect(self) -> None:
        self.client.disconnect()

    @connection_required
    def get_status(self) -> None:
        self.client.get_status()

    @connection_required
    def get_element_value(self, element : str) -> dict or str:
        return self.client.INIT_STRUCTURE[element]

    @connection_required
    def get_history_data(self, seccode : str, board : str, period : int, count : int, reset : bool = True) -> dict or str:
        reset = 'true' if reset else 'false'
        data = ";".join([seccode, board, str(period), str(count), reset]) + "\0"

        return self.client.get_history_data(data)

    @connection_required
    def get_forts_position(self, client : str = "") -> dict or str:

        return self.client.get_forts_position(client)

    @connection_required
    def get_client_limits(self, client : str) -> dict or str:

        return self.client.get_client_limits(client)

    @connection_required
    def get_markets(self) -> dict or str:

        return self.client.get_markets()

    @connection_required
    def get_servtime_difference(self) -> None:

        self.client.get_servtime_difference()

    @connection_required
    def get_connector_version(self) -> dict or str:

        return self.client.get_connector_version()

    @connection_required
    def get_server_id(self) -> dict or str:

        return self.client.get_server_id()

    @connection_required
    def change_pass(self) -> None:

        self.client.change_pass()

    @connection_required
    def neworder(self, seccode : str, board : str, client : str, union : str, quantity : str, 
                buysell : BuySell, brokerref : str, price : str = 'market', hidden : str = '', 
                unfilled : OrderType = OrderType.PutInQueue, usecredit : str = '',
                nosplit : str = '', expdate : str = '') -> None:

        if price == 'market':
            price = ''
            bymarket = 'true'

        else:
            bymarket = ''

        # union = self.get_element_value('client')[client]['union']

        data = ';'.join([board, seccode, client, union, price, hidden, quantity, buysell, bymarket, brokerref, unfilled, usecredit, nosplit, expdate]) + '\0'
        self.client.neworder(data)

    @connection_required
    def newcondorder(self, seccode : str, board : str, client : str, union : str, quantity : str, 
                buysell : BuySell, brokerref : str, cond_type : CondType, cond_value : str = '',
                valid_after : str = "0", valid_before : str = "0",
                price : str = 'market', hidden : str = '', within_pos : str = '', 
                usecredit : str = '', nosplit : str = '', expdate : str = '') -> None:

        if price == 'market':
            price = ''
            bymarket = 'true'
        else:
            bymarket = ''

        data = ';'.join([board, seccode, client, union, price, hidden, quantity, buysell, bymarket, brokerref, cond_type,\
            cond_value, valid_after, valid_before, usecredit, within_pos, nosplit, expdate]) + '\0'
        self.client.newcondorder(data)

    @connection_required
    def newstoporder(self, seccode : str, board : str, client : str, union : str,
                    buysell : BuySell, stoploss_activationprice : str, stoploss_quantity : str,
                    takeprofit_activationprice : str, takeprofit_quantity : str,
                    linkedorderno : str = '', validfor : str = '',
                    expdate : str = '', stoploss_orderprice : str = 'market',
                    takeprofit_correction : str = '', takeprofit_spread : str = '', stoploss_usecredit : str = '',
                    takeprofit_usecredit : str = '', stoploss_guardtime : str = '',
                    takeprofit_guardtime : str = '', stoploss_brokerref : str = '',
                    takeprofit_brokerref : str = '', takeprofit_bymarket : bool = False) -> None:

        if stoploss_orderprice == 'market':
            stoploss_orderprice = ''
            stoploss_bymarket = 'true'

        else:

            stoploss_bymarket = ''

        if takeprofit_bymarket:
            takeprofit_bymarket = 'true'

        else:
            takeprofit_bymarket = ''

        data = ';'.join([board, seccode, client, union, buysell, linkedorderno, validfor, expdate,\
            stoploss_activationprice, stoploss_orderprice, stoploss_bymarket, stoploss_quantity, stoploss_usecredit, stoploss_guardtime, stoploss_brokerref,\
            takeprofit_activationprice, takeprofit_quantity, takeprofit_usecredit, takeprofit_guardtime, takeprofit_brokerref, \
                takeprofit_correction, takeprofit_spread, takeprofit_bymarket]) + '\0'
        self.client.newstoporder(data)

    @connection_required
    def cancelorder(self, orderid : str) -> None:
        self.client.cancelorder(orderid)

    @connection_required
    def cancelstoporder(self, orderid : str) -> None:
        self.client.cancelstoporder(orderid)

    @connection_required
    def get_securities_info(self, market : str, seccode : str) -> dict or str:
        data = ','.join([market, seccode])
        return self.client.get_securities_info(data)

    @connection_required
    def moveorder(self, orderid : str, price : str, moveflag : str, quantitiy : str) -> None:

        data = ','.join([orderid, price, moveflag, quantitiy])
        self.client.moveorder(data)

    @connection_required
    def get_united_equity(self, union : str) -> dict or str:

        return self.client.get_united_equity(union)

    @connection_required
    def get_united_go(self, union : str) -> dict or str:

        return self.client.get_united_go(union)

    @connection_required
    def get_mc_portfolio(self, client : str = '', union : str = '', currency : bool = False, 
                        asset : bool = False, money : bool = False, depo : bool = False, registers :bool = False, maxbs : bool = False) -> dict or str or None:

        if len(client) == 0 and len(union) == 0:
            print('Set either client or union')
            return

        currency = 'true' if currency else 'false'
        asset = 'true' if asset else 'false'
        money = 'true' if money else 'false'
        depo = 'true' if depo else 'false'
        registers = 'true' if registers else 'false'
        maxbs = 'true' if maxbs else 'false'

        data = ','.join([client, union, currency, asset, money, depo, registers, maxbs])

        return self.client.get_mc_portfolio(data)

    @connection_required
    def get_max_buy_sell(self, seccodes : str, markets : str, client : str = '', union : str = '') -> dict or str or None:
        if len(client) == 0 and len(union) == 0:
            print('Set either client or union')
            return

        if len(markets) != len(seccodes):
            print('The number of seccodes shall equals the number of markets')
            return

        data = ';'.join([client, union, ','.join(markets), ','.join(seccodes)])

        return self.client.get_max_buy_sell(data)

    @connection_required
    def get_cln_sec_permissions(self, seccode : str, board : str, client : str = '', union : str = '') -> dict or str or None:
        if len(client) == 0 and len(union) == 0:
            print('Set either client or union')
            return

        data = ','.join([board, seccode, client, union])

        return self.client.get_cln_sec_permissions(data)