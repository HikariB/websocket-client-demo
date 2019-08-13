function footerStyle(column) {
    return {
        id: {
            classes: 'uppercase'
        },
        name: {
            css: {'font-weight': 'normal'}
        },
        price: {
            css: {color: 'red'}
        }
    }[column.field]
}

function cellStyle(value, row, index) {
    var classes = [
        'bg-blue',
        'bg-green',
        'bg-orange',
        'bg-yellow',
        'bg-red'
    ];

    if (value > 0) {
        return {
            classes: classes[1]
        }
    }
    return {
        css: {
            color: 'red'
        }
    }
}

function delayWarnStyle(value, row, index) {
    var classes = [
        'bg-blue',
        'bg-green',
        'bg-orange',
        'bg-yellow',
        'bg-red'
    ];
    if (value > 10) {
        return {
            css: {
                color: 'red'
            }
        }
    }
    // -1000 表示初始化
    // -2000 表示已连接
    // -3000 表示关闭
    // -4000 表示Error
    if (value === -3000 || value === -4000) {
        return classes[4];
    }
    return {
        css: {
            color: 'green'
        }
    }

}

function delayDF(value) {
    if (value === -1)
        return "Init..."
    if (value < -10)
        return '<span style="color: #85144B">异常</span>';
    return value;
}


function sumFormatterFraction(data) {
    let field = this.field;
    let total_sum = data.reduce(function (sum, row) {
        return (sum) + (row[field] || 0)
    }, 0);
    return toPercent(total_sum)
}


function toPercent(point) {
    if (point > 1 || point < 0)
        return 'NaN';
    var str = Number(point * 100).toFixed(3);
    str += "%";
    return str;
}

function accountName(value) {
    if (value === '83925101')
        return '<a href="monitor-detail?id=83925101">GTW</a>';
    if (value === '83925105')
        return '<a href="monitor-detail?id=83925105">GTL</a>';
    if (value === '118933')
        return '<a href="monitor-detail?id=118933">BCX</a>';
    if (value === '83925087')
        return '<a href="monitor-detail?id=83925087">GTZ</a>';
    if (value === '11803017')
        return '<a href="monitor-detail?id=11803017">XZY</a>';
    if (value === '20087058')
        return '<a href="monitor-detail?id=20087058">WTY</a>';
    if (value === '20092132')
        return '<a href="monitor-detail?id=20092132">WTQ</a>';
    if (value === '26000616')
        return '<a href="monitor-detail?id=26000616">FZT</a>';
    return value;
}


var MDValidFlag = true;


function responseHandler(res) {
    // console.log(res[0].profitSum);
    MDValidFlag = true;
    res.forEach(function (e) {
        if (!e.marketDataValid) {
            MDValidFlag = false;
        }
    });

    if (MDValidFlag) {

    } else {

    }

    return res;
}


$(function () {

    $.ajax({
        type: "get",
        url: "login-result",
        success: function (data) {
            console.log(data);
        }
    });

    $.ajax({
        type: "get",
        url: "sub-result",
        success: function (data) {
            console.log(data);
        }
    });

    let summary = $('#summary')

    $('#disconnect').click(btnlogout);

    summary.bootstrapTable();
});
