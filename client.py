import socket
from multiprocessing import Process
from tools import xml2dict
from threading import Thread, Timer
import os

class TransaqCSharp(Process):

    def __init__(self, ports, *args, **kwargs):
        super(TransaqCSharp, self).__init__(*args, **kwargs)
        self.ports = ports
        self._target = self.run_server

    def run_server(self):
        os.system("Transaq\\Transaq\\bin\\x86\\Debug\\netcoreapp3.1\\Transaq.exe " + " ".join([str(p) for p in self.ports]))
            

class Connector(socket.socket):
    def __init__(self, address, port):
        super(Connector, self).__init__()
        self.address = address
        self.port = port

    def connect2server(self):
        while True:
            try:
                self.connect((self.address, self.port))
                break
            except ConnectionRefusedError:
                continue

class Ping(Connector):

    def connect2server(self):
        super(Ping, self).connect2server()
        self.ping()

    def ping(self):
        def _ping():
            while True:
                try:
                    self.recv(4)
                except ConnectionResetError:
                    break
                except ConnectionAbortedError:
                    print("HERE")
                    break
        Thread(target=_ping, daemon=True).start()

class News(Connector):

    def connect2server(self, callback):
        super(News, self).connect2server()

        self.callback = callback
        self.subscribe_news()

    def subscribe_news(self):

        def _subscribe_news():
            while True:
                try:
                    msg = ''
                    while '\0' not in msg:
                        msg += str(self.recv(1), 'utf-8')
                    section = msg.split(":")[0]
                    bufLength = int(msg.split(":")[1].replace('\0', '')) + 1

                    msg = str(self.recv(bufLength), 'utf-8')
                    while '\0' not in msg:
                        msg += str(self.recv(1), 'utf-8')
                    msg = msg.replace('\0', '')
                    news = xml2dict(section, msg)

                    if self.callback is not None:
                        self.callback(news)

                except ConnectionResetError:
                    break
                except ConnectionAbortedError:
                    break

        Thread(target=_subscribe_news, daemon=True).start()

class Sub(Connector):
    def __init__(self, address, port):
        super(Sub, self).__init__(address, port)
        self._is_connected = False

        self.subscription = {method : {
            'status' : False,
            'pairs' : [],
            'callback' : None,
        } for method in ['quotations', 'alltrades', 'quotes', 'ticks']}

    def connect2server(self):
        super(Sub, self).connect2server()
        self._is_connected = True

    def subscribe(self, method, pairs, callback):

        def handle():
            try:
                while True in [self.subscription[m]['status'] for m in self.subscription]:
                    try:
                        msg = ''
                        while "\0" not in msg:
                            msg += str(self.recv(1), 'utf-8')
                        section = msg.split(":")[0]
                        bufLength = int(msg.split(":")[1].replace('\0', '')) + 1

                        msg = str(self.recv(bufLength), 'utf-8')
                        while '\0' not in msg:
                            msg += str(self.recv(1), 'utf-8')
                        msg = msg.replace('\0', '')

                        event = xml2dict(section, msg)

                        event = {section: event}

                        if self.subscription[method]['callback'] is not None:
                            self.subscription[method]['callback'](event)
                    except ConnectionAbortedError:
                        continue
            except ConnectionResetError:
                pass
            except ConnectionAbortedError:
                pass

        if method == 'ticks':
            self.subscription[method]['pairs'] = []
        for p in pairs:
            self.subscription[method]['pairs'].append(p)
        self.subscription[method]['status'] = True
        self.subscription[method]['callback'] = callback

        if not self._is_connected:
            self.connect2server()
            Thread(target=handle, args=(), daemon=True).start()

    def unsubscribe(self, method, pairs):
        if method == 'ticks':
            self.subscription[method]['pairs'] = []
        for p in pairs:
            self.subscription[method]['pairs'].remove(p)
        if len(self.subscription[method]['pairs']) == 0:
            self.subscription[method]['status'] = False
        if not True in [self.subscription[m]['status'] for m in self.subscription]:
            self._is_connected = False
            self.close()

class TData(Connector):
    
    def receive_data(self, timeout=None):
        try:
            msg = ""
            if timeout is not None:
                self.settimeout(1.0)
            try:
                while '\0' not in msg:
                    msg += str(self.recv(1), 'utf-8')
            except socket.timeout:
                return 

            section = msg.split(":")[0]
            bufLength = int(msg.split(":")[1].replace('\0', '')) + 1

            bmsg = self.recv(bufLength)
            while True:
                try:
                    msg = str(bmsg, 'utf-8')
                    break
                except UnicodeDecodeError:
                    bmsg += self.recv(1)

            while '\0' not in msg:
                bmsg = self.recv(1)
                while True:
                    try:
                        msg += str(bmsg, 'utf-8')
                        break
                    except UnicodeDecodeError:
                        bmsg += self.recv(1)
            msg = msg.replace('\0', '')

            return [section, msg]
        except ConnectionResetError:
            pass
        except ConnectionAbortedError:
            pass

class AccData(Connector):
    def connect2server(self, callback):
        super(AccData, self).connect2server()

        self.subscribe_account_event(callback=callback)

    def subscribe_account_event(self, callback):

        def _subscribe_account_event():
            while True:
                try:
                    msg = ''
                    while '\0' not in msg:
                        msg += str(self.recv(1), 'utf-8')
                    section = msg.split(":")[0]
                    bufLength = int(msg.split(":")[1].replace('\0', '')) + 1

                    msg = str(self.recv(bufLength), 'utf-8')
                    while '\0' not in msg:
                        msg += str(self.recv(1), 'utf-8')
                    msg = msg.replace('\0', '')
                    acc_event = xml2dict(section, msg)

                    if callback is not None:
                        callback(acc_event)
                except ConnectionResetError:
                    break

                except ConnectionAbortedError:
                    break

        Thread(target=_subscribe_account_event, daemon=True).start()

class Client(Connector):
    def __init__(self, address, ports, news_callback=None, account_callback=None):
        super(Client, self).__init__(address, ports[0])
        self.INIT_STRUCTURE = {}
        
        self._is_connected = False
        self.ping = Ping(self.address, ports[1])
        self.news = News(self.address, ports[2])
        self.sub = Sub(self.address, ports[3])
        self.tdata = TData(self.address, ports[4])
        self.accdata = AccData(self.address, ports[5])
        self.news_callback = news_callback
        self.account_callback = account_callback
        self.ports = ports

    def process_request(self, name, ret=False):
        try:
            msg = ""
            while "\0" not in msg:
                try:
                    msg += str(self.recv(1024), 'utf-8')
                except UnicodeDecodeError:
                    print('Undefined error (%s)' % name)
                    self.settimeout(1.0)
                    try:
                        while self.recv(1024): pass
                    except socket.timeout:
                        pass

                    if ret:
                        data = self.tdata.receive_data(2.0)

                        if data is None:
                            return {}

                        else:
                            return xml2dict(*data)

                    else:
                        break

            if len(msg) != 0:
                msg = xml2dict('result', msg.replace('\0', ''))
                print('Response (%s):' % name, msg)

                return msg

            return None
        except ConnectionResetError:
            pass
        except ConnectionAbortedError:
            pass
    
    def connect2server(self):

        self.tproc = TransaqCSharp(ports=self.ports, daemon=True)
        self.tproc.start()
        super(Client, self).connect2server()

        self.ping.connect2server()
        self.tdata.connect2server()

        response = ""
        while "\0" not in response:
            response += str(self.recv(1), 'utf-8')
        print(response.replace('\0', ''))

    def close_connection(self):
        self.ping.close()
        self.news.close()
        self.sub.close()
        self.tdata.close()
        self.accdata.close()
        self.tproc.terminate()
        self.close()

    def connect2transaq(self, login):

        def fill_struct(section, msg):
            if section not in self.INIT_STRUCTURE.keys():
                if section != 'client':
                    self.INIT_STRUCTURE[section] = msg.replace('</%s>'%section, '')
                else:
                    self.INIT_STRUCTURE[section] = msg
            else:
                if section != 'client':
                    self.INIT_STRUCTURE[section] += msg.replace('<%s>'%section, '').replace('</%s>'%section, '')
                else:
                    self.INIT_STRUCTURE[section] += msg
            
        password = input('Input password: ')

        self.send(bytes('auth:%s;%s\0'%(login,password), 'utf-8'))
        msg = self.process_request(self.connect2transaq.__name__, True)
        if msg['result']['success'] == 'true':
            data = self.tdata.receive_data()
            section, msg = data

            if section == 'server_status':
                print('Authorization error: ' + xml2dict(section, msg)['error'])

            else:
                print('Authorization success.')
                self.send(bytes('start_acc:\0', 'utf-8'))
                self.news.connect2server(callback=self.news_callback)
                self.accdata.connect2server(callback=self.account_callback)
                fill_struct(section, msg)
                self._is_connected = True

        if self._is_connected:
            while True:
                data = self.tdata.receive_data(1.0)
                if data is None:
                    break
                section, msg = data

                fill_struct(section, msg)

            print('Processing initial structure...')
            for section in self.INIT_STRUCTURE:
                if section == 'client':
                    self.INIT_STRUCTURE[section] = '<clients>'+self.INIT_STRUCTURE[section]+'</clients>'
                elif section in ['union', 'overnight', 'server_status']:
                    pass 
                else:
                    self.INIT_STRUCTURE[section] += '</%s>' % section
                self.INIT_STRUCTURE[section] = xml2dict(section=section, xml=self.INIT_STRUCTURE[section])

            print('Transaq initial elements: ' + ', '.join([elm for elm in self.INIT_STRUCTURE]))
            return True

        return False

    def get_old_news(self, count, callback):
        self.news.callback = callback
        self.send(bytes('old_news:%s\0' % str(count), 'utf-8'))
        self.process_request(self.get_old_news.__name__)
        def f():
            self.news.callback = self.news_callback
        Timer(5000, f).start()

    def get_news_by_id(self, news_id, callback):
        self.news.callback = callback
        self.send(bytes('get_news:%s\0' % news_id, 'utf-8'))
        self.process_request(self.get_news_by_id.__name__)
        def f():
            self.news.callback = self.news_callback
        Timer(5000, f).start()

    def subscribe(self, method, data, pairs, callback=None): 
        self.send(bytes(method + ":" + data, 'utf-8'))
        msg = self.process_request(self.subscribe.__name__, True)
        if msg['result']['success'] == 'true':
            print("SUBSCRIBE: " + method, pairs)
            self.sub.subscribe(method, pairs, callback)

    def subscribe_ticks(self, data, pair, callback=None):
        self.send(bytes("ticks:" + data, 'utf-8'))
        msg = self.process_request(self.subscribe_ticks.__name__, True)
        if msg['result']['success'] == 'true':
            print("SUBSCRIBE: ticks", pair)
            self.sub.subscribe('ticks', [pair], callback)

    def unsubscribe(self, method, data, pairs):
        self.send(bytes("-" + method + ":" + data, 'utf-8'))
        msg = self.process_request(self.unsubscribe.__name__, True)
        if msg['result']['success'] == 'true':
            self.sub.unsubscribe(method, pairs)

    def unsubscribe_ticks(self):
        self.send(bytes("-ticks:\0", 'utf-8'))
        msg = self.process_request(self.unsubscribe_ticks.__name__, True)
        if msg['result']['success'] == 'true':
            self.sub.unsubscribe('ticks', [])

    def get_history_data(self, data):
        self.send(bytes("history_data:" + data, 'utf-8'))

        msg = self.process_request(self.get_history_data.__name__, True)
        if msg['result']['success'] == 'true':
            data = self.tdata.receive_data(5.0)
            if data is None:
                return {}
            else:
                return xml2dict(*data)

    def get_forts_position(self, client):
        self.send(bytes("forts_position:" + client + '\0', 'utf-8'))

        msg = self.process_request(self.get_forts_position.__name__, True)
        if msg['result']['success'] == 'true':

            data = self.tdata.receive_data(2.0)

            if data is None:
                return {}

            else:
                return data

    def get_client_limits(self, client):
        self.send(bytes("client_limits:" + client + '\0', 'utf-8'))

        msg = self.process_request(self.get_client_limits.__name__, True)
        if msg['result']['success'] == 'true':

            data = self.tdata.receive_data(2.0)

            if data is None:
                return {}

            else:
                return xml2dict(*data)

    def get_markets(self):
        self.send(bytes("markets:\0", 'utf-8'))

        msg = self.process_request(self.get_markets.__name__, True)
        if msg['result']['success'] == 'true':

            data = self.tdata.receive_data(2.0)
            return xml2dict(*data)

    def get_servtime_difference(self):
        self.send(bytes("servtime_dif:\0", 'utf-8'))

        self.process_request(self.get_servtime_difference.__name__)

    def change_pass(self):
        old_pass = input("Input old password: ")
        new_pass = input("Input new password: ")

        self.send(bytes("change_pass:" + old_pass + "," + new_pass + '\0', 'utf-8'))

        self.process_request(self.change_pass.__name__)

    def get_connector_version(self):
        self.send(bytes("get_version:\0", 'utf-8'))

        msg = self.process_request(self.get_connector_version.__name__, True)

        if msg['result']['success'] == 'true':
            data = self.tdata.receive_data(2.0)
            return xml2dict(*data)
    
    def get_server_id(self):
        self.send(bytes("get_sid:\0", 'utf-8'))

        msg = self.process_request(self.get_server_id.__name__, True)

        if msg['result']['success'] == 'true':
            data = self.tdata.receive_data(2.0)
            return xml2dict(*data)

    def neworder(self, data):
        self.send(bytes("neworder:" + data, 'utf-8'))

        self.process_request(self.neworder.__name__)

    def newcondorder(self, data):
        self.send(bytes("newcondorder:" + data, 'utf-8'))

        self.process_request(self.newcondorder.__name__)

    def newstoporder(self, data):
        self.send(bytes("newstoporder:" + data, 'utf-8'))

        self.process_request(self.newstoporder.__name__)

    def cancelorder(self, orderid):
        self.send(bytes("cancelorder:" + orderid + "\0", 'utf-8'))

        self.process_request(self.cancelorder.__name__)

    def cancelstoporder(self, orderid):
        self.send(bytes("cancelstoporder:" + orderid + "\0", 'utf-8'))

        self.process_request(self.cancelstoporder.__name__)

    def moveorder(self, data):
        self.send(bytes("moveorder:" + data + "\0", 'utf-8'))

        self.process_request(self.moveorder.__name__)

    def get_securities_info(self, data):
        self.send(bytes("sec_info:" + data + "\0", 'utf-8'))

        msg = self.process_request(self.get_securities_info.__name__, True)

        if msg['result']['success'] == 'true':

            data = self.tdata.receive_data(2.0)

            if data is None:
                return {}

            else:
                return xml2dict(*data)

    def get_united_equity(self, union):
        self.send(bytes("united_equity:" + union + "\0", 'utf-8'))

        msg = self.process_request(self.get_united_equity.__name__, True)

        if msg['result']['success'] == 'true':

            data = self.tdata.receive_data(2.0)

            if data is None:
                return {}

            else:
                return xml2dict(*data)

    def get_united_go(self, union):
        self.send(bytes("united_go:" + union + "\0", 'utf-8'))

        msg = self.process_request(self.get_united_go.__name__, True)

        if msg['result']['success'] == 'true':

            data = self.tdata.receive_data(2.0)

            if data is None:
                return {}

            else:
                return xml2dict(*data)

    def get_mc_portfolio(self, data):
        self.send(bytes("mc_portfolio:" + data + "\0", 'utf-8'))

        msg = self.process_request(self.get_mc_portfolio.__name__, True)

        if msg['result']['success'] == 'true':

            data = self.tdata.receive_data(2.0)

            if data is None:
                return {}

            else:
                return xml2dict(*data)

    def get_max_buy_sell(self, data):
        self.send(bytes("max_buy_sell:" + data + "\0", 'utf-8'))

        msg = self.process_request(self.get_max_buy_sell.__name__, True)

        if msg['result']['success'] == 'true':

            data = self.tdata.receive_data(2.0)

            if data is None:
                return {}

            else:
                return data

    def get_cln_sec_permissions(self, data):
        self.send(bytes("cln_sec_permissions:" + data + "\0", 'utf-8'))

        msg = self.process_request(self.get_cln_sec_permissions.__name__, True)

        if msg['result']['success'] == 'true':

            data = self.tdata.receive_data(2.0)

            if data is None:
                return {}

            else:
                return xml2dict(*data)

    def disconnect(self):
        self.send(bytes("disconnect:\0", 'utf-8'))
        msg = self.process_request(self.disconnect.__name__, True)
        if msg['result']['success'] == 'true':
            section, msg = self.tdata.receive_data()

            if self.sub._is_connected:
                self.sub.close()
            self.news.close()
            self.accdata.close()
            self._is_connected = False

            print('You are disconnected')

    def get_status(self):
        self.send(bytes("status:\0", 'utf-8'))

        msg = self.process_request(self.get_status.__name__, True)
        if msg['result']['success'] == 'true':
            while True:
                data = self.tdata.receive_data(1.0)
                if data is None:
                    break
                print(data[0], xml2dict(*data))