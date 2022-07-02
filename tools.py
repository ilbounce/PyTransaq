import xml.etree.ElementTree as ET

def xml2dict(section, xml):
    try:
        if section == 'markets':
            struct = ET.fromstring(xml)
            dct = {}

            for child in struct:
                dct[child.attrib['id']] = child.text

        elif section == "client":
            structs = ET.fromstring(xml)
            dct = {}

            cur_id = ""
            for struct in structs:
                attr = struct.attrib

                for ak in attr:
                    if ak == 'id':
                        cur_id = attr[ak]
                        dct[cur_id] = {}
                    else:
                        dct[cur_id][ak] = attr[ak]

                for child in struct:
                    dct[cur_id][child.tag] = child.text

        elif section in ['sec_info_upd']:
            struct = ET.fromstring(xml)
            dct = {}

            num = -1
            for child in struct:
                if child.tag == "secid" or child.tag == "id":
                    num += 1
                    dct[num] = {}
                dct[num][child.tag] = child.text
        
        elif section in ['boards', 'candlekinds', 'securities', 'pits']:
            structs = ET.fromstring(xml)
            dct = {}
            for num, struct in enumerate(structs):
                attr = struct.attrib
                attr_keys = list(attr.keys())
                dct[num] = {}
                for ak in attr_keys:
                    dct[num][ak] = attr[ak]
                for child in struct:
                    dct[num][child.tag] = child.text

        elif section == 'quotations':
            structs = ET.fromstring(xml)
            dct = {}

            for struct in structs:
                secid = struct.attrib['secid']
                dct[secid] = {}
                for child in struct:
                    dct[secid][child.tag] = child.text

        elif section == 'alltrades':
            structs = ET.fromstring(xml)
            dct = {}

            for struct in structs:
                secid = struct.attrib['secid']
                cur_trade = {}
                if secid not in dct:
                    dct[secid] = []
                for child in struct:
                    cur_trade[child.tag] = child.text
                dct[secid].append(cur_trade)

        elif section == 'quotes':
            structs = ET.fromstring(xml)
            dct = {}
            for struct in structs:
                secid = struct.attrib['secid']
                if secid not in dct:
                    dct[secid] = {'ask' : [],
                                    'bid' : []}
                dct[secid]['seccode'] = struct.find('seccode').text
                dct[secid]['board'] = struct.find('board').text
                if struct.find('buy') is not None:

                    dct[secid]['bid'].append([float(struct.find('price').text), struct.find('yield').text, struct.find('buy').text, struct.find('source').text])

                elif struct.find('sell') is not None:
                    dct[secid]['ask'].append([float(struct.find('price').text), struct.find('yield').text, struct.find('sell').text, struct.find('source').text])

            for elm in dct:

                if len(dct[elm]['ask']) > 1:
                    dct[elm]['ask'] = sorted(dct[elm]['ask'], key = lambda x: x[0])
                if len(dct[elm]['bid']) > 1:
                    dct[elm]['bid'] = sorted(dct[elm]['bid'], key = lambda x: -x[0])

        elif section in ['orders', 'trades']:
            structs = ET.fromstring(xml)
            dct = {}
            for struct in structs:

                ths = struct.tag

                dct[ths] = {}
                for (k,v) in struct.attrib.items():
                    dct[ths][k] = v

                for child in struct:
                    dct[ths][child.tag] = child.text

        elif section == 'sec_info':
            struct = ET.fromstring(xml)
            dct = struct.attrib

            for child in struct:
                dct[child.tag] = child.text

        elif section in ['united_equity', 'united_go', 'mc_portfolio']:
            struct = ET.fromstring(xml)
            dct = struct.attrib

            for child in struct:
                dct[child.tag] = child.text

        elif section == 'ticks':
            struct = ET.fromstring(xml)[0]
            dct = {
                section : {}
            }

            for child in struct:
                dct[section][child.tag] = child.text

        elif section == 'server_status':
            try:
                struct = ET.fromstring(xml)
            except ET.ParseError:
                struct = ET.fromstring(xml + '</%s>'%section)
            dct = {}
            attr = struct.attrib

            for ak in attr.keys():
                dct[ak] = attr[ak]

            if dct['connected'] == 'error':
                dct['error'] = struct.text

        elif section in ['news_header', 'news_body']:
            struct = ET.fromstring(xml)
            dct = {}

            for child in struct:
                dct[child.tag] = child.text

        elif section == 'candles':
            struct = ET.fromstring(xml)
            dct = {}

            attr = struct.attrib
            for ak in attr:
                dct[ak] = attr[ak]

            dct['candles'] = {}
            for n, child in enumerate(struct):
                attr = child.attrib
                dct['candles'][n] = {}
                for ak in attr:
                    dct['candles'][n][ak] = attr[ak]

        elif section == 'clientlimits':
            struct = ET.fromstring(xml)
            client = struct.attrib['client']
            dct = {
                client : {}
            }

            for child in struct:
                dct[client][child.tag] = child.text

        elif section == 'result':
            struct = ET.fromstring(xml)
            dct = {
                section : {}
            }

            dct[section] = struct.attrib
            for child in struct:
                dct[section][child.tag] = child.text

        elif section == 'connector_version':
            struct = ET.fromstring(xml)
            dct = {
                section : {}
            }
            dct[section] = struct.text

        elif section == 'current_server':
            struct = ET.fromstring(xml)
            dct = {
                section : {}
            }

            dct[section] = struct.attrib

        elif section == 'max_buy_sell':
            structs = ET.fromstring(xml)
            dct = structs.attrib

            dct['securities'] = {}
            for n, struct in enumerate(structs):
                ths = struct.tag + str(n)
                dct[ths] = struct.attrib

                for child in struct:
                    dct[ths][child.tag] = child.text

        elif section == 'cln_sec_permissions':
            struct = ET.fromstring(xml)
            dct = struct.attrib

            for child in struct:

                if child.tag == 'security':
                    dct[child.tag] = {}

                    for cchild in child:
                        dct[child.tag][cchild.tag] = cchild.text
                else:
                    dct[child.tag] = child.text

        else:
            struct = ET.fromstring(xml)
            dct = {}
            attr = struct.attrib

            for ak in attr.keys():
                dct[ak] = attr[ak]

        return dct

    except ET.ParseError:
        return xml
    
