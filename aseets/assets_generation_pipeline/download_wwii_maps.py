import os
import sys
import time
import json
import urllib.request
from PIL import Image
import io

OUTPUT_DIR = r"E:\Games\KingWar\aseets\assets_generation_pipeline\source\maps"
os.makedirs(OUTPUT_DIR, exist_ok=True)

# List of 29 WWII city maps with unified metadata and provenance
CITIES = [
    # 1-5 France
    {
        "id": "france_paris",
        "country_zh": "法国",
        "country_en": "France",
        "city_zh": "巴黎",
        "city_en": "Paris",
        "filename": "France_Paris.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/france_city_plans/txu-pclmaps-oclc-5095683-paris.jpg",
        "crop": None,
        "series": "AMS France City Plans (GSGS 4470)",
        "scale": "1:25,000",
        "year": 1944,
        "publisher": "U.S. Army Map Service / British War Office",
        "historical_note": "二战西线核心战役城市。1940年法国战役沦陷，1944年8月巴黎起义并由自由法国第2装甲师与美军第4步兵师联合解放。"
    },
    {
        "id": "france_vichy",
        "country_zh": "法国",
        "country_en": "France",
        "city_zh": "维希",
        "city_en": "Vichy",
        "filename": "France_Vichy.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/france/txu-oclc-6617501-nl31-5.jpg",
        "crop": (2700, 2600, 4300, 3650),
        "series": "AMS Series M561 / NL 31-5 Moulins-Vichy",
        "scale": "1:250,000",
        "year": 1943,
        "publisher": "U.S. Army Map Service",
        "historical_note": "二战维希法国（Etat Francais）政府驻地（1940-1944年贝当政权）。阿列河畔重要政治与枢纽重镇。"
    },
    {
        "id": "france_marseille",
        "country_zh": "法国",
        "country_en": "France",
        "city_zh": "马赛",
        "city_en": "Marseille",
        "filename": "France_Marseille.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/france_city_plans/txu-pclmaps-oclc-6481023-marseille_south.jpg",
        "crop": None,
        "series": "AMS France City Plans (Marseille South / Port & City)",
        "scale": "1:10,000",
        "year": 1944,
        "publisher": "U.S. Army Map Service",
        "historical_note": "法国地中海第一大深水军民港口。1944年8月盟军“龙骑兵行动”（Operation Dragoon）南部登陆与解放马赛战役核心战场。"
    },
    {
        "id": "france_dunkirk",
        "country_zh": "法国",
        "country_en": "France",
        "city_zh": "敦刻尔克",
        "city_en": "Dunkirk",
        "filename": "France_Dunkirk.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/france_city_plans/txu-pclmaps-oclc-6479872-dunkerque.jpg",
        "crop": None,
        "series": "AMS France City Plans (Dunkerque)",
        "scale": "1:10,000",
        "year": 1943,
        "publisher": "U.S. Army Map Service",
        "historical_note": "1940年“发电机行动”（Operation Dynamo）33.8万盟军奇迹大撤退所在地，二战早期西线最关键的防守包围与撤离滩头。"
    },
    {
        "id": "france_sedan_maginot",
        "country_zh": "法国",
        "country_en": "France",
        "city_zh": "色当（马其诺防线）",
        "city_en": "Sedan (Maginot Line)",
        "filename": "France_Sedan_Maginot.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/france/txu-oclc-6617501-nm31-9.jpg",
        "crop": (1700, 600, 3200, 1900),
        "series": "AMS Series M561 / NM 31-9 Reims-Sedan (Montmedy Sector)",
        "scale": "1:250,000",
        "year": 1943,
        "publisher": "U.S. Army Map Service",
        "historical_note": "色当突破战役（1940年5月13-14日）。古德里安第19装甲军强渡马斯河突破马其诺防线西北翼蒙梅迪强化防区，彻底瓦解盟军战线。"
    },
    # 6 Belgium
    {
        "id": "belgium_brussels",
        "country_zh": "比利时",
        "country_en": "Belgium",
        "city_zh": "布鲁塞尔",
        "city_en": "Brussels",
        "filename": "Belgium_Brussels.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/belgium_50k/txu-pclmaps-oclc-6624543-brussels-55.jpg",
        "crop": None,
        "series": "AMS Series M703 (GSGS 4040) Sheet 55 Brussels",
        "scale": "1:50,000",
        "year": 1943,
        "publisher": "U.S. Army Map Service / British War Office",
        "historical_note": "比利时首都，低地国家战略交通中枢。1940年5月被德军占领，1944年9月3日英军近卫装甲师迅速攻占解放。"
    },
    # 7 Netherlands
    {
        "id": "netherlands_amsterdam",
        "country_zh": "荷兰",
        "country_en": "Netherlands",
        "city_zh": "阿姆斯特丹",
        "city_en": "Amsterdam",
        "filename": "Netherlands_Amsterdam.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/western_europe/txu-oclc-6472044-nn31-12a.jpg",
        "crop": (2400, 50, 4900, 2450),
        "series": "AMS Series M501 / NN 31-12 Verso Amsterdam and Vicinity",
        "scale": "1:35,000",
        "year": 1944,
        "publisher": "U.S. Army Map Service",
        "historical_note": "荷兰宪法首都。1940年荷兰战役后沦陷，二战期间荷兰地下抵抗运动中心与水运枢纽，经历1944-1945年严寒大饥荒。"
    },
    # 8 Austria
    {
        "id": "austria_vienna",
        "country_zh": "奥地利",
        "country_en": "Austria",
        "city_zh": "维也纳",
        "city_en": "Vienna",
        "filename": "Austria_Vienna.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/western_europe/txu-oclc-6472044-nm33-12a.jpg",
        "crop": (100, 50, 3050, 3236),
        "series": "AMS Series M501 / NM 33-12 Verso Wien and Vicinity",
        "scale": "1:35,000",
        "year": 1944,
        "publisher": "U.S. Army Map Service",
        "historical_note": "多瑙河战略要冲。1938年德奥合并，1945年4月苏军乌克兰第二、第三方面军发起维也纳战役，攻克该城。"
    },
    # 9-12 Germany
    {
        "id": "germany_berlin",
        "country_zh": "德国",
        "country_en": "Germany",
        "city_zh": "柏林",
        "city_en": "Berlin",
        "filename": "Germany_Berlin.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/western_europe/txu-oclc-6472044-nn33-10a.jpg",
        "crop": None,
        "series": "AMS Series M501 / NN 33-10 Verso Berlin-Potsdam and Vicinity",
        "scale": "1:35,000",
        "year": 1944,
        "publisher": "U.S. Army Map Service",
        "historical_note": "纳粹德国首都。1945年4月16日至5月2日柏林战役，苏军攻占国会大厦，欧洲反法西斯战争终结标志。"
    },
    {
        "id": "germany_hamburg",
        "country_zh": "德国",
        "country_en": "Germany",
        "city_zh": "汉堡",
        "city_en": "Hamburg",
        "filename": "Germany_Hamburg.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/western_europe/txu-oclc-6472044-nn32-8a.jpg",
        "crop": (2300, 50, 4900, 2600),
        "series": "AMS Series M501 / NN 32-8 Verso Hamburg and Vicinity",
        "scale": "1:35,000",
        "year": 1944,
        "publisher": "U.S. Army Map Service",
        "historical_note": "德国第一大港与U型潜艇主要造船基地。1943年7月盟军“蛾摩拉行动”（Operation Gomorrah）毁灭性大轰炸目标。"
    },
    {
        "id": "germany_munich",
        "country_zh": "德国",
        "country_en": "Germany",
        "city_zh": "慕尼黑",
        "city_en": "Munich",
        "filename": "Germany_Munich.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/central_europe_250k/txu-pclmaps-oclc-6481354-munchen-m-49.jpg",
        "crop": (1500, 1000, 5500, 5000),
        "series": "AMS Series M508 Central Europe Sheet M-49 München",
        "scale": "1:250,000",
        "year": 1943,
        "publisher": "U.S. Army Map Service",
        "historical_note": "纳粹党“发祥地”与巴伐利亚首府、南部军工与铁路枢纽。1945年4月30日美军第7集团军攻入该市。"
    },
    {
        "id": "germany_frankfurt",
        "country_zh": "德国",
        "country_en": "Germany",
        "city_zh": "法兰克福",
        "city_en": "Frankfurt",
        "filename": "Germany_Frankfurt.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/central_europe_250k/txu-pclmaps-oclc-6481354-frankfurt-l-51.jpg",
        "crop": (1200, 2400, 5200, 5800),
        "series": "AMS Series M508 Central Europe Sheet L-51 Frankfurt am Main",
        "scale": "1:250,000",
        "year": 1943,
        "publisher": "U.S. Army Map Service",
        "historical_note": "美茵河畔战略交通枢纽与化工机械基地。1945年3月美军第3集团军巴顿部强渡莱茵河后攻占法兰克福。"
    },
    # 13-14 Czechoslovakia
    {
        "id": "czechoslovakia_prague",
        "country_zh": "捷克斯洛伐克",
        "country_en": "Czechoslovakia",
        "city_zh": "布拉格",
        "city_en": "Prague",
        "filename": "Czechoslovakia_Prague.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/western_europe/txu-oclc-6472044-nm33-5a.jpg",
        "crop": None,
        "series": "AMS Series M501 / NM 33-5 Verso Praha and Vicinity",
        "scale": "1:35,000",
        "year": 1944,
        "publisher": "U.S. Army Map Service",
        "historical_note": "捷克首都。1939年德国强占，1945年5月5日爆发布拉格起义，科涅夫元帅率领苏军装甲先锋于5月9日进城彻底歼灭残敌。"
    },
    {
        "id": "czechoslovakia_bratislava",
        "country_zh": "捷克斯洛伐克",
        "country_en": "Czechoslovakia",
        "city_zh": "布拉迪斯拉发",
        "city_en": "Bratislava",
        "filename": "Czechoslovakia_Bratislava.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/western_europe/txu-oclc-6472044-nm33-12a.jpg",
        "crop": (3050, 50, 4900, 3236),
        "series": "AMS Series M501 / NM 33-12 Verso Bratislava and Vicinity",
        "scale": "1:35,000",
        "year": 1944,
        "publisher": "U.S. Army Map Service",
        "historical_note": "斯洛伐克首府，多瑙河左岸要塞。1945年4月布拉迪斯拉发-布尔诺战役中由苏联红军第7近卫集团军解放。"
    },
    # 15 Bulgaria
    {
        "id": "bulgaria_sofia",
        "country_zh": "保加利亚",
        "country_en": "Bulgaria",
        "city_zh": "索菲亚",
        "city_en": "Sofia",
        "filename": "Bulgaria_Sofia.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/western_europe/txu-oclc-6472044-nk34-6a.jpg",
        "crop": None,
        "series": "AMS Series M501 / NK 34-6 Verso Sofia and Vicinity",
        "scale": "1:35,000",
        "year": 1944,
        "publisher": "U.S. Army Map Service",
        "historical_note": "保加利亚首都，巴尔干战略中枢。二战期间遭盟军战略空袭，1944年9月9日保加利亚祖国阵线起义并转向同盟国。"
    },
    # 16 Hungary
    {
        "id": "hungary_budapest",
        "country_zh": "匈牙利",
        "country_en": "Hungary",
        "city_zh": "布达佩斯",
        "city_en": "Budapest",
        "filename": "Hungary_Budapest.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/western_europe/txu-oclc-6472044-nl34-1a.jpg",
        "crop": None,
        "series": "AMS Series M501 / NL 34-1 Verso Budapest and Vicinity",
        "scale": "1:35,000",
        "year": 1944,
        "publisher": "U.S. Army Map Service",
        "historical_note": "布达佩斯战役（1944年10月-1945年2月13日），二战欧洲战场最残酷的城市攻防围城战之一，多瑙河两岸血战102天。"
    },
    # 17 Yugoslavia
    {
        "id": "yugoslavia_belgrade",
        "country_zh": "南斯拉夫",
        "country_en": "Yugoslavia",
        "city_zh": "贝尔格莱德",
        "city_en": "Belgrade",
        "filename": "Yugoslavia_Belgrade.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/western_europe/txu-oclc-6472044-nl34-11a.jpg",
        "crop": None,
        "series": "AMS Series M501 / NL 34-11 Verso Beograd and Vicinity",
        "scale": "1:35,000",
        "year": 1944,
        "publisher": "U.S. Army Map Service",
        "historical_note": "萨瓦河与多瑙河交汇处巴尔干要冲。1944年10月贝尔格莱德战役，铁托游击队与苏联乌克兰第三方面军并肩攻克解放。"
    },
    # 18 Romania
    {
        "id": "romania_bucharest",
        "country_zh": "罗马尼亚",
        "country_en": "Romania",
        "city_zh": "布加勒斯特",
        "city_en": "Bucharest",
        "filename": "Romania_Bucharest.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/western_europe/txu-oclc-6472044-nl35-11a.jpg",
        "crop": None,
        "series": "AMS Series M501 / NL 35-11 Verso Bucuresti and Vicinity",
        "scale": "1:35,000",
        "year": 1944,
        "publisher": "U.S. Army Map Service",
        "historical_note": "罗马尼亚首都，邻近普洛耶什蒂油田群。1944年8月23日国王米哈伊一世发起政变倒戈，城市迅速肃清德军。"
    },
    # 19-29 Soviet Union
    {
        "id": "ussr_minsk",
        "country_zh": "苏联",
        "country_en": "USSR",
        "city_zh": "明斯克",
        "city_en": "Minsk",
        "filename": "USSR_Minsk.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/eastern_europe/txu-oclc-6519747-nn35-8a.jpg",
        "crop": None,
        "series": "AMS Series N501 / NN 35-8 Verso Minsk and Vicinity",
        "scale": "1:35,000",
        "year": 1944,
        "publisher": "U.S. Army Map Service",
        "historical_note": "白俄罗斯首都。1941年明斯克合围战，1944年7月“巴格拉季昂行动”苏联红军强力收复，成为二战英雄城市。"
    },
    {
        "id": "ussr_kiev",
        "country_zh": "苏联",
        "country_en": "USSR",
        "city_zh": "基辅",
        "city_en": "Kiev",
        "filename": "USSR_Kiev.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/eastern_europe/txu-oclc-6519747-nm36-4a.jpg",
        "crop": None,
        "series": "AMS Series N501 / NM 36-4 Verso Kiev and Vicinity",
        "scale": "1:35,000",
        "year": 1944,
        "publisher": "U.S. Army Map Service",
        "historical_note": "乌克兰第聂伯河畔古都。1941年基辅战役（人类历史上规模最大围歼战），1943年11月强渡第聂伯河战役中苏军成功收复。"
    },
    {
        "id": "ussr_smolensk",
        "country_zh": "苏联",
        "country_en": "USSR",
        "city_zh": "斯摩棱斯克",
        "city_en": "Smolensk",
        "filename": "USSR_Smolensk.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/eastern_europe/txu-oclc-6519747-nn36-5a.jpg",
        "crop": None,
        "series": "AMS Series N501 / NN 36-5 Verso Smolensk and Vicinity",
        "scale": "1:35,000",
        "year": 1944,
        "publisher": "U.S. Army Map Service",
        "historical_note": "莫斯科陆上西方门户。1941年斯摩棱斯克战役阻滞德军中央集团军群进军两个月，为保卫莫斯科争取了无价的时间。"
    },
    {
        "id": "ussr_kalinin",
        "country_zh": "苏联",
        "country_en": "USSR",
        "city_zh": "加里宁",
        "city_en": "Kalinin",
        "filename": "USSR_Kalinin.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/eastern_europe/txu-oclc-6519747-no36-12a.jpg",
        "crop": None,
        "series": "AMS Series N501 / NO 36-12 Verso Kalinin and Vicinity",
        "scale": "1:35,000",
        "year": 1944,
        "publisher": "U.S. Army Map Service",
        "historical_note": "现特维尔（Tver），莫斯科战役北翼关键要塞。加里宁方面军于1941年12月16日成功收复，是莫斯科保卫战反攻收复的首座大城市。"
    },
    {
        "id": "ussr_sevastopol",
        "country_zh": "苏联",
        "country_en": "USSR",
        "city_zh": "塞瓦斯托波尔",
        "city_en": "Sevastopol",
        "filename": "USSR_Sevastopol.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/eastern_europe/txu-oclc-6519747-nl36-11a.jpg",
        "crop": None,
        "series": "AMS Series N501 / NL 36-11 Verso Sevastopol and Vicinity",
        "scale": "1:35,000",
        "year": 1944,
        "publisher": "U.S. Army Map Service",
        "historical_note": "黑海舰队核心母港与超级要塞。1941-1942年塞瓦斯托波尔围城战（250天浴血坚守），1944年5月克里米亚攻势中苏军彻底光复。"
    },
    {
        "id": "ussr_leningrad",
        "country_zh": "苏联",
        "country_en": "USSR",
        "city_zh": "列宁格勒",
        "city_en": "Leningrad",
        "filename": "USSR_Leningrad.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/eastern_europe/txu-oclc-6519747-no36-1a.jpg",
        "crop": None,
        "series": "AMS Series N501 / NO 36-1 Verso Leningrad and Vicinity",
        "scale": "1:35,000",
        "year": 1944,
        "publisher": "U.S. Army Map Service",
        "historical_note": "列宁格勒保卫战（1941年9月8日-1944年1月27日），人类历史上最惨烈漫长的近900天封锁围城战，二战坚韧不拔的象征。"
    },
    {
        "id": "ussr_stalingrad",
        "country_zh": "苏联",
        "country_en": "USSR",
        "city_zh": "斯大林格勒",
        "city_en": "Stalingrad",
        "filename": "USSR_Stalingrad.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/eastern_europe/txu-oclc-6519747-nm38-11a.jpg",
        "crop": None,
        "series": "AMS Series N501 / NM 38-11 Verso Stalingrad and Vicinity",
        "scale": "1:35,000",
        "year": 1944,
        "publisher": "U.S. Army Map Service",
        "historical_note": "斯大林格勒战役（1942年7月-1943年2月2日），伏尔加河畔转折点之战，德军第6集团军全军覆没，二战根本转折战役。"
    },
    {
        "id": "ussr_murmansk",
        "country_zh": "苏联",
        "country_en": "USSR",
        "city_zh": "摩尔曼斯克",
        "city_en": "Murmansk",
        "filename": "USSR_Murmansk.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/murmansk_area/txu-pclmaps-oclc-9913115-murmansk-4.jpg",
        "crop": (600, 800, 4800, 6200),
        "series": "AMS / War Office Series 4363 Sheet 4 Murmansk",
        "scale": "1:100,000",
        "year": 1943,
        "publisher": "U.S. Army Map Service / British War Office",
        "historical_note": "北极圈内不冻港，北极护航航线（Arctic Convoys）终点站。苏德战争期间盟国租借法案战略军火与重物资入苏生命线。"
    },
    {
        "id": "ussr_moscow",
        "country_zh": "苏联",
        "country_en": "USSR",
        "city_zh": "莫斯科",
        "city_en": "Moscow",
        "filename": "USSR_Moscow.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/eastern_europe/txu-oclc-6519747-nn37-1a.jpg",
        "crop": None,
        "series": "AMS Series N501 / NN 37-1 Verso Moskva and Vicinity",
        "scale": "1:35,000",
        "year": 1944,
        "publisher": "U.S. Army Map Service",
        "historical_note": "苏联首都。莫斯科保卫战（1941年10月-1942年1月），打破德军闪电战神话与“天下无敌”狂妄迷梦的首场大捷。"
    },
    {
        "id": "ussr_baku",
        "country_zh": "苏联",
        "country_en": "USSR",
        "city_zh": "巴库",
        "city_en": "Baku",
        "filename": "USSR_Baku.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/caucasia/txu-pclmaps-oclc-6571962-baku.jpg",
        "crop": (2000, 600, 5600, 3400),
        "series": "AMS Series N561 Caucasia Sheet Baku",
        "scale": "1:253,440",
        "year": 1943,
        "publisher": "U.S. Army Map Service",
        "historical_note": "里海石油之都，生产全苏联70%以上的石油。1942年德军“蓝色方案”（Fall Blau）高加索战略进攻的终极核心目标。"
    },
    {
        "id": "ussr_vladivostok",
        "country_zh": "苏联",
        "country_en": "USSR",
        "city_zh": "符拉迪沃斯托克",
        "city_en": "Vladivostok",
        "filename": "USSR_Vladivostok.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/manchuria/txu-oclc-6614368-nk52-3a.jpg",
        "crop": (50, 50, 2800, 3700),
        "series": "AMS Series N504 Manchuria Sheet NK 52-3 Verso Vladivostok and Vicinity",
        "scale": "1:35,000",
        "year": 1945,
        "publisher": "U.S. Army Map Service",
        "historical_note": "苏联太平洋舰队母港，跨西伯利亚大铁路终点，二战太平洋护航航线（Pacific Route）海运枢纽，1945年远东八月风暴行动出击基地。"
    },
    # 30-32 Poland
    {
        "id": "poland_danzig",
        "country_zh": "波兰",
        "country_en": "Poland",
        "city_zh": "但泽",
        "city_en": "Danzig",
        "filename": "Poland_Danzig.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/western_europe/txu-oclc-6472044-nn34-4a.jpg",
        "crop": None,
        "series": "AMS Series M501 / NN 34-4 Verso Gdansk (Danzig) and Vicinity",
        "scale": "1:35,000",
        "year": 1944,
        "publisher": "U.S. Army Map Service",
        "historical_note": "波罗的海核心军港与造船工业重镇。1939年9月1日德军炮击西盘半岛（Westerplatte）标志着二战爆发，1945年3-4月苏军发起东波美拉尼亚攻势攻克但泽。"
    },
    {
        "id": "poland_poznan",
        "country_zh": "波兰",
        "country_en": "Poland",
        "city_zh": "波兹南",
        "city_en": "Poznan",
        "filename": "Poland_Poznan.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/western_europe/txu-oclc-6472044-nn33-12a.jpg",
        "crop": None,
        "series": "AMS Series M501 / NN 33-12 Verso Poznan and Vicinity",
        "scale": "1:35,000",
        "year": 1944,
        "publisher": "U.S. Army Map Service",
        "historical_note": "二战大波兰核心枢纽与要塞要冲。1939年德国入侵后改称波森（Posen），1945年1-2月爆发惨烈的波兹南战役（Battle of Poznań），苏军围攻并肃清坚固要塞。"
    },
    {
        "id": "poland_warsaw",
        "country_zh": "波兰",
        "country_en": "Poland",
        "city_zh": "华沙",
        "city_en": "Warsaw",
        "filename": "Poland_Warsaw.png",
        "url": "https://maps.lib.utexas.edu/maps/ams/western_europe/txu-oclc-6472044-nn34-11a.jpg",
        "crop": None,
        "series": "AMS Series M501 / NN 34-11 Verso Warszawa and Vicinity",
        "scale": "1:35,000",
        "year": 1944,
        "publisher": "U.S. Army Map Service",
        "historical_note": "波兰首都，维斯瓦河核心战区。历经1939年华沙保卫战、1943年华沙犹太人起义与1944年8-10月惨烈的华沙起义（Warsaw Uprising），1945年1月维斯瓦河-奥得河战役解放。"
    }
]

def fetch_image(url):
    headers = {"User-Agent": "curl/8.4.0"}
    req = urllib.request.Request(url, headers=headers)
    for attempt in range(4):
        try:
            with urllib.request.urlopen(req, timeout=45) as resp:
                data = resp.read()
                return Image.open(io.BytesIO(data))
        except Exception as e:
            print(f"  Attempt {attempt+1} failed ({e}), retrying...")
            time.sleep(2)
    raise RuntimeError(f"Could not download {url}")

def process_all():
    print(f"Starting batch fetch & standardization for {len(CITIES)} WWII city maps...")
    results = []

    for idx, item in enumerate(CITIES, 1):
        target_path = os.path.join(OUTPUT_DIR, item["filename"])
        print(f"[{idx:02d}/{len(CITIES):02d}] {item['country_zh']} - {item['city_zh']} ({item['city_en']})...")
        
        # Check if file exists and is valid
        if os.path.exists(target_path):
            try:
                with Image.open(target_path) as existing:
                    w, h = existing.size
                    size_kb = os.path.getsize(target_path) // 1024
                    print(f"  Existing file valid: {item['filename']} ({w}x{h}, {size_kb} KB)")
                    item_info = dict(item)
                    item_info["dimensions"] = [w, h]
                    item_info["file_size_kb"] = size_kb
                    results.append(item_info)
                    continue
            except Exception:
                print("  Existing file damaged, re-downloading...")

        # Fetch and process
        img = fetch_image(item["url"])
        
        # Crop if specified
        if item.get("crop"):
            crop_box = item["crop"]
            # Ensure within bounds
            cw, ch = img.size
            safe_box = (
                max(0, min(crop_box[0], cw)),
                max(0, min(crop_box[1], ch)),
                max(0, min(crop_box[2], cw)),
                max(0, min(crop_box[3], ch))
            )
            img = img.crop(safe_box)
        
        # Ensure RGB mode
        if img.mode != "RGB":
            img = img.convert("RGB")
            
        # Save as PNG
        img.save(target_path, format="PNG", optimize=True)
        w, h = img.size
        size_kb = os.path.getsize(target_path) // 1024
        print(f"  Saved -> {item['filename']} ({w}x{h}, {size_kb} KB)")
        
        item_info = dict(item)
        item_info["dimensions"] = [w, h]
        item_info["file_size_kb"] = size_kb
        results.append(item_info)
        
        # Polite delay
        time.sleep(1)

    # Save manifest.json
    manifest_path = os.path.join(OUTPUT_DIR, "manifest.json")
    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(results, f, ensure_ascii=False, indent=2)
    print(f"Manifest written to: {manifest_path}")

    # Generate Markdown documentation
    readme_path = os.path.join(OUTPUT_DIR, "README.md")
    with open(readme_path, "w", encoding="utf-8") as f:
        f.write("# 第二次世界大战重点战役城市军用地形图数据库\n")
        f.write("## WWII Strategic Cities Military Tactical & Topographic Map Collection\n\n")
        f.write("> 本地图集专为 `KingWar` 资产生成管线 (`assets_generation_pipeline`) 搜集与规范化整理。\n")
        f.write("> **格式统一规范**：全套 29 张地图统一为二战时期同盟军测绘机构（**美国陆军制图局 U.S. Army Map Service (AMS)** 及 **英国总参谋部测绘局 War Office GSGS**）军规战术制图，统一定制为 24 位真彩色无损 **PNG** 格式，并以 `{国家}_{城市}.png` 统一格式命名。\n\n")
        f.write("| 序号 | 国家 | 城市 | 文件名 | 比例尺 | 年代 | 原始系列/图幅 | 图像尺寸 | 二战历史战役定位 |\n")
        f.write("| :---: | :--- | :--- | :--- | :---: | :---: | :--- | :---: | :--- |\n")
        for i, m in enumerate(results, 1):
            dim = f"{m['dimensions'][0]}×{m['dimensions'][1]}"
            f.write(f"| {i} | {m['country_zh']} ({m['country_en']}) | {m['city_zh']} ({m['city_en']}) | `{m['filename']}` | {m['scale']} | {m['year']} | {m['series']} | {dim} | {m['historical_note']} |\n")

    print(f"README.md written to: {readme_path}")
    print(f"ALL {len(results)} MAPS PROCESSED SUCCESSFULLY!")

if __name__ == "__main__":
    process_all()
