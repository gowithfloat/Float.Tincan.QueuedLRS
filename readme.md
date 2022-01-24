## Float.TinCan.QueuedLRS [![Test](https://github.com/gowithfloat/Float.TinCan.QueuedLRS/actions/workflows/test.yml/badge.svg)](https://github.com/gowithfloat/Float.TinCan.QueuedLRS/actions/workflows/test.yml) [![NuGet](https://img.shields.io/nuget/v/Float.TinCan.QueuedLRS)](https://www.nuget.org/packages/Float.TinCan.QueuedLRS/)

The `QueuedLRS` is an LRS queue for holding or batching statements before passing onto another `ILRS` implementation (e.g. `RemoteLRS`). Statements are queued locally until the target LRS confirms successful receipt of the statements. Any queued statements are also written to disk so that the queue can persist across sessions.

The queue will store statements indefinitely until the queue has been flushed. At that point, a batch of statements will be forwarded to the target  LRS. If the statements were successfully received, then those statements are removed from the local queue. If an error occurs, those statements will be kept in the queue and sent again later.

`QueuedLRS` is a great option for a mobile application looking to store statements offline and send to an LRS when an internet connection is available.

## Building

This project can be built using [Visual Studio for Mac](https://visualstudio.microsoft.com/vs/mac/) or [Cake](https://cakebuild.net/). It is recommended that you build this project by invoking the bootstrap script:

    ./build.sh

There are a number of optional arguments that can be provided to the bootstrapper that will be parsed and passed on to Cake itself. See the [Cake build file](./build.cake) in order to identify all supported parameters.

    ./build.sh \
        --task=Build \
        --projectName=Float.TinCan.QueuedLRS \
        --configuration=Debug \
        --nugetUrl=https://nuget.org \
        --nugetToken=####

## Installing

### NuGet
This library is available as a NuGet via nuget.org.

## Usage

### Quick Start

    using Float.TinCan;
    using Float.TinCan.QueuedLRS;

    var remoteLRS = new RemoteLRS("http://example.com/xapi-endpoint/", "my app id", "my app secret");
    var storePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "statement-store.json");

    var queuedLRS = new QueuedLRS(remoteLRS, new JSONStatementStore(storePath));

Statements can now be queued by calling:

    queuedLRS.SaveStatement(myStatement);

The queued statement will be stored locally and sent to the LRS when the queue is flushed.

### Flushing the Queue

“Flushing” the queue refers to attempting to send queued statements to the target LRS. Applications can manually flush the queue by calling `FlushStatementQueueWithResult()`.

When the queue is flushed, a batch of statements (by default, 50 at at time) are sent to the target LRS.

Applications can also define [Triggers](#Triggers) for automatically flushing the queue at various points during it’s lifecycle.

Additionally, the queue is automatically flushed when querying statements from the LRS

### Triggers

The queue is automatically flushed when any of the defined triggers (`IQueueFlushTrigger`) is fired. Applications can define their own triggers, but three triggers are included by default:

* **PeriodicTrigger** — sends statements to the LRS periodically (e.g. every 1 minute)
* **CompletedStatementTrigger** — sends statements to the LRS when a statement is stored that has the `completed` verb
* **InternetConnectionTrigger** — sends statements to the LRS when an network connection becomes available after previously being unavailable

### Statement Validation

Because statements will not be sent to the LRS until later, statements that are queued are put through a rudimentary validation test to ensure that they are properly formatted.

However, even with performing a local validation of statements, it is possible that the target LRS will still reject the statements when the queue is being flushed. This is currently an unrecoverable error for the queued LRS and these statements must be discarded.

## License

All content in this repository is shared under an MIT license. See [license.md](./license.md) for details.
